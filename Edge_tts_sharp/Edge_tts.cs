using Edge_tts_sharp.Model;
using Edge_tts_sharp.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketState = System.Net.WebSockets.WebSocketState;


namespace Edge_tts_sharp;

public class Edge_tts
{
    /// <summary>
    /// 调试模式
    /// </summary>
    public static bool Debug = false;
    /// <summary>
    /// 同步模式
    /// </summary>
    public static bool Await = false;

    private const string ChromiumVersion = "143.0.3650.75";
    private const string ChromiumMajorVersion = "143";

    private static Dictionary<string, string> Headers { get; } =
        new()
        {
            { "User-Agent", $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{ChromiumMajorVersion}.0.0.0 Safari/537.36 Edg/{ChromiumMajorVersion}.0.0.0" },
            // { "Accept-Encoding", "gzip, deflate, br, zstd" },
            { "Accept-Language", "en-US,en;q=0.9" },
            { "Pragma", "no-cache" },
            { "Cache-Control", "no-cache" },
            { "Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold"},
            { "Accept", "*/*" },
        };

    private static ArraySegment<byte> GetArraySegmentFromString(string s) => new(Encoding.UTF8.GetBytes(s));


    private static string GenerateSecMsGecToken()
    {
        // 来自 https://github.com/rany2/edge-tts/issues/290#issuecomment-2464956570
        var ticks = DateTime.Now.ToFileTimeUtc();
        ticks -= ticks % 3_000_000_000;
        var str = ticks + "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        return ToHexString(HashData(Encoding.ASCII.GetBytes(str)));
    }

    private static string ToHexString(byte[] byteArray)
    {
        return BitConverter.ToString(byteArray).Replace("-", "").ToUpper();
    }

    private static byte[] HashData(byte[] data)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(data);
            return hashBytes;
        }
    }

    private static string GetTimestamp()
    {
        var utc = DateTimeOffset.UtcNow;
        return utc.ToString(
            "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'",
            CultureInfo.InvariantCulture
        );
    }

    static string GetGUID()
    {
        return Guid.NewGuid().ToString().Replace("-", "");
    }

    /// <summary>
    /// 讲一个浮点型数值转换为百分比数值
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    static string FromatPercentage(double input)
    {
        string output;

        if (input < 0)
        {
            output = input.ToString("+#;-#;0") + "%";
        }
        else
        {
            output = input.ToString("+#;-#;0") + "%";
        }
        return output;
    }

    static string ConvertToAudioFormatWebSocketString(string outputformat)
    {
        return "Content-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"" + outputformat + "\"}}}}";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lang">输出语言</param>
    /// <param name="voice">音源名</param>
    /// <param name="rate">语速，-100% - 100% 之间的值，无需传递百分号</param>
    /// <param name="text"></param>
    /// <returns></returns>
    static string ConvertToSsmlText(string lang, string voice, int rate, int volume, string text)
    {
        return $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'  xml:lang='{lang}'><voice name='{voice}'><prosody pitch='+0Hz' rate='{FromatPercentage(rate)}' volume='{volume}'>{text}</prosody></voice></speak>";
    }

    static string ConvertToSsmlWebSocketString(string requestId, string lang, string voice, int rate, int volume, string msg)
    {
        return $"X-RequestId:{requestId}\r\nContent-Type:application/ssml+xml\r\nX-Timestamp:{GetTimestamp()}Z\r\nPath:ssml\r\n\r\n{ConvertToSsmlText(lang, voice, rate, volume, msg)}";
    }

    /// <summary>
    /// 语言转文本，将结果返回到回调函数中
    /// </summary>
    /// <param name="option">播放参数</param>
    /// <param name="voice">音源参数</param>
    public static async Task InvokeAsync(PlayOption option, eVoice voice, Action<List<byte>> callback, IProgress<List<byte>> progress = null)
    {
        var binary_delim = "Path:audio\r\n";
        var sendRequestId = GetGUID();
        var binary = new List<byte>();
        bool IsTurnEnd = false;

        var ws = new ClientWebSocket()
        {
            Options =
            {
                Cookies = new CookieContainer()
                {

                }
            }
        };
        foreach (var header in Headers)
        {
            ws.Options.SetRequestHeader(header.Key, header.Value);
        }

        var uri = new Uri(
                      $"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
                      $"?TrustedClientToken=6A5AA1D4EAFF4E9FB37E23D68491D6F4" +
                      $"&ConnectionId={sendRequestId}" +
                  $"&Sec-MS-GEC={GenerateSecMsGecToken()}" +
                  $"&Sec-MS-GEC-Version=1-{ChromiumVersion}" +
                  "");
        // ws.Options.HttpVersion = HttpVersion.Version20;
        // ws.Options.Proxy = new WebProxy(new Uri("http://127.0.0.1:8888"));
        ws.Options.Cookies.SetCookies(new Uri("https://speech.platform.bing.com"), $"muid={Guid.NewGuid().ToString().ToUpper().Replace("-", "")};");
        var tcs = new TaskCompletionSource<object>();
        var listenTask = Task.Run(async () =>
        {
            // 用于接收网络流的底层缓冲区
            var bufferArray = new byte[1024 * 32];
            var bufferSegment = new ArraySegment<byte>(bufferArray);

            // 用于组装完整 WebSocket 消息的临时容器（处理分片）
            var messageBuffer = new List<byte>();

            while (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting)
            {
                if (ws.State == WebSocketState.Connecting)
                {
                    await Task.Delay(10);
                    continue;
                }

                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(bufferSegment, CancellationToken.None);
                }
                catch (Exception)
                {
                    break; // 连接断开
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                    break;
                }

                // 将当前收到的片段加入临时容器
                // 注意：必须只取 result.Count 长度
                messageBuffer.AddRange(bufferArray.Take(result.Count));

                // 关键点：只有当 EndOfMessage 为 true 时，才表示“当前这一帧”数据收全了
                if (result.EndOfMessage)
                {
                    var fullMessageBytes = messageBuffer.ToArray(); // 转为数组进行处理
                    messageBuffer.Clear(); // 清空容器准备接收下一条消息

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(fullMessageBytes);
                        // Console.WriteLine($"收到文本: {text}");
                        if (text.Contains("Path:turn.end"))
                        {
                            IsTurnEnd = true;
                            // 这里通常不需要 Close，除非你想读完一句就断开
                            // await ws.CloseAsync(...); 
                            break;
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // 1. 基础长度检查
                        if (fullMessageBytes.Length < 2) continue;

                        // 2. 解析头部长度 (Big Endian)
                        var headerLength = (ushort)((fullMessageBytes[0] << 8) | fullMessageBytes[1]);

                        // 3. 校验数据完整性 (防止索引越界)
                        if (fullMessageBytes.Length < headerLength + 2)
                        {
                            // 这种情况理论上不应该发生（因为我们已经判断了 EndOfMessage），
                            // 但如果发生了，说明协议解析有问题，不能简单的 continue 丢弃，
                            // 不过 Edge TTS 协议通常很标准。
                            continue;
                        }

                        // 4. 提取音频 (跳过 2字节Length + HeaderBody)
                        // 只有音频数据部分才需要加入到 binary 列表
                        var audioStartIndex = 2 + headerLength;
                        var audioLength = fullMessageBytes.Length - audioStartIndex;

                        if (audioLength > 0)
                        {
                            var audioData = new byte[audioLength];
                            Array.Copy(fullMessageBytes, audioStartIndex, audioData, 0, audioLength);
                            binary.AddRange(audioData);
                        }
                    }
                }
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            // 循环结束后处理回调和保存
            if (binary.Count > 0)
            {
                // 确保回调存在
                callback?.Invoke(binary);

                if (!string.IsNullOrEmpty(option.SavePath))
                {
                    // 建议使用 FileShare.ReadWrite 防止占用
                    await Task.Run(() => File.WriteAllBytes(option.SavePath, binary.ToArray()));
                }
            }
        });
        await ws.ConnectAsync(uri, CancellationToken.None);
        await ws.SendAsync(GetArraySegmentFromString(ConvertToAudioFormatWebSocketString(voice.SuggestedCodec)), WebSocketMessageType.Text, true, CancellationToken.None);
        await ws.SendAsync(GetArraySegmentFromString(ConvertToSsmlWebSocketString(sendRequestId, voice.Locale, voice.Name, option.Rate, ((int)option.Volume * 100), option.Text)), WebSocketMessageType.Text, true, CancellationToken.None);
        await listenTask;
        ws.Dispose();
    }

    /// <summary>
    /// 另存为mp3文件
    /// </summary>
    /// <param name="option">播放参数</param>
    /// <param name="voice">音源参数</param>
    public static async Task SaveAudioAsync(PlayOption option, eVoice voice)
    {
        if (string.IsNullOrEmpty(option.SavePath))
        {
            throw new Exception("保存路径为空，请核对参数后重试.");
        }
        await InvokeAsync(option, voice, null);
    }

    /// <summary>
    /// 调用微软Edge接口，文字转语音并直接播放
    /// </summary>
    /// <param name="option">播放参数</param>
    /// <param name="voice">音源参数</param>
    public static async Task PlayTextAsync(PlayOption option, eVoice voice)
    {
        List<byte> audioData = null;

        await InvokeAsync(option, voice, (data) =>
        {
            audioData = data;
        });

        if (audioData != null && audioData.Count > 0)
        {
            await Audio.PlayToByteAsync(audioData.ToArray(), option.Volume);
        }
    }

    /// <summary>
    /// 获取支持的音频列表
    /// </summary>
    /// <returns></returns>
    public static List<eVoice> GetVoice()
    {
        var voiceList = Tools.GetEmbedText("Edge_tts_sharp.Source.VoiceList.json");
        return Tools.StringToJson<List<eVoice>>(voiceList);
    }
}
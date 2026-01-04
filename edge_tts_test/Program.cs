// See https://aka.ms/new-console-template for more information
using Edge_tts_sharp;
using Edge_tts_sharp.Model;

Edge_tts.Await = true;
PlayOption option = new PlayOption
{
    Rate = 0,
    Text = ""
};
string msg = string.Empty;
Console.WriteLine("请输入文本内容.");
option.Text = Console.ReadLine();
// 获取xiaoxiao语音包
var voice = Edge_tts.GetVoice()[55];
// 文字转语音，并且设置语速
await Edge_tts.PlayTextAsync(option, voice);
Console.WriteLine("自动输出");
Console.ReadLine();


// 保存音频
static async Task SaveAudio()
{
    PlayOption option = new PlayOption
    {
        Rate = 0,
        Text = "Hello EdgeTTs",
        SavePath = "C:\\audio.mp3"
    };
    var voice = Edge_tts.GetVoice().FirstOrDefault(i => i.Name == "Microsoft Server Speech Text to Speech Voice (zh-CN, XiaoxiaoNeural)");
    await Edge_tts.SaveAudioAsync(option, voice);
}

// 文本转语音
static async Task TextToAudio()
{
    PlayOption option = new PlayOption
    {
        Rate = 0,
        Text = "Hello EdgeTTs",
    };
    var voice = Edge_tts.GetVoice().First();
    await Edge_tts.PlayTextAsync(option, voice);
}

// 自定义接口使用
static async Task MyFunc(string msg, eVoice voice)
{
    PlayOption option = new PlayOption
    {
        Rate = 0,
        Text = msg,
    };
    await Edge_tts.InvokeAsync(option, voice, libaray =>
    {
        // 写入自己的操作
        // ...
    });
}
# Edge_tts_sharp 
[Edge_tts_sharp](https://www.nuget.org/packages/Edge_tts_sharp)，是一个免费的C#库，调用Microsoft Edge Text to Speech接口生成音频。

## install
```sh
NuGet\Install-Package Edge_tts_sharp
```
## 方法

### 全局对象
| 参数 | 说明 |
| --- | --- |
| Edge_tts.Debug | 调试模式，为true则显示日志 |
| Edge_tts.Await | 同步模式，为true会等待函数执行完毕 | 

### Invoke/PlayTextAsync/SaveAudioAsync 方法
| 参数 | 说明 |
| --- | --- |
| PlayOption | 参数配置 |
| eVoice | 音源 |
| Action<List<callback>> | 回调函数，参数是一个binary数组 |

### PlayOption 对象
| 名称  | 说明 |
| --- | --- |
| Text | 播放的文本 |
| Rate | 播放速度，是一个-100至+100的数值 |
| Volume | 音量，是一个0-1的浮点数值 |
| SavePath | 音频保存路径，为空不保存 |

## 文字转语音
```cs
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
```

## 获取音频列表
```cs
using Edge_tts_sharp;

var voices = Edge_tts.GetVoice();
foreach(var item in voices){
    Console.WriteLine($"voice name is{item.Name}, locale（语言） is {item.Locale}, SuggestedCodec(音频类型) is {item.SuggestedCodec}");
}
```
## 汉语语音包有：

| ShortName              | Locale       | 地区         |
|------------------------|--------------|--------------|
| zh-HK-HiuGaaiNeural    | zh-HK        | 香港         |
| zh-HK-HiuMaanNeural    | zh-HK        | 香港         |
| zh-HK-WanLungNeural    | zh-HK        | 香港         |
| zh-CN-XiaoxiaoNeural   | zh-CN        | 中国（大陆） |
| zh-CN-XiaoyiNeural     | zh-CN        | 中国（大陆） |
| zh-CN-YunjianNeural    | zh-CN        | 中国（大陆） |
| zh-CN-YunxiNeural      | zh-CN        | 中国（大陆） |
| zh-CN-YunxiaNeural     | zh-CN        | 中国（大陆） |
| zh-CN-YunyangNeural    | zh-CN        | 中国（大陆） |
| zh-CN-liaoning-XiaobeiNeural | zh-CN-liaoning | 中国（辽宁） |
| zh-TW-HsiaoChenNeural  | zh-TW        | 台湾         |
| zh-TW-YunJheNeural     | zh-TW        | 台湾         |
| zh-TW-HsiaoYuNeural    | zh-TW        | 台湾         |
| zh-CN-shaanxi-XiaoniNeural | zh-CN-shaanxi | 中国（陕西） |

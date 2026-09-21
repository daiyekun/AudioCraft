#### 这是一个音频转换工具比如从awb转换成mp3

### 我使用的是 ffmpeg.exe 。但是我并没有上传。如果需要请下载exe 放置到tools目录下



# 构建

~~~~
- 程序已发布到 bin\Release\net10.0-windows\win-x64\publish\
需要安装 Inno Setup
1. 下载安装: https://jrsoftware.org/isdl.php
2. 安装后重新运行构建脚本:
powershell -ExecutionPolicy Bypass -File build.ps1
或者您可以直接使用发布目录中的文件，它是自包含的，无需安装 .NET 运行时即可运行。
~~~~


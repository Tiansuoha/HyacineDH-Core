# HyacineDH-Core

**__项目开发中__**

[EN](Docs/README_en-US.md) | [繁中](Docs/README_zh-TW.md) | [JP](Docs/README_ja-JP.md)

## 📖介绍
特征：支持游戏的4.0版本
加入我们的聊天群组：[Discord](https://discord.gg/castoriceps)

## 📦快速开始
要求：.NET 9.0 [点击下载](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
```bash
git clone https://github.com/DBKAHHK/HyacineDH-Core.git
cd HyacineDH-Core
dotnet run
```

## 📝使用说明
如果你不想从源代码编译，请使用 [Releases](https://github.com/DBKAHHK/HyacineDH-Core/releases) 下载预编译的文件。
如果你使用的是termux，你可以在终端执行如下指令，这会下载执行该仓库中的run.sh

```bash
sed -i 's@^\(deb.*stable main\)$@#\1\ndeb https://mirrors.cernet.edu.cn/termux/apt/termux-main stable main@' $PREFIX/etc/apt/sources.list
apt update && apt upgrade -y
apt install -y curl
curl -L -o run.sh https://gh-proxy.org/raw.githubusercontent.com/Tiansuoha/HyacineDH-Core/main/run.sh
chmod +x run.sh
./run.sh
```

## 💡功能

- [√] **商店**
- [√] **编队**
- [√] **抽卡** 
- [√] **战斗** 
- [√] **场景** 
- [√] **基本的角色培养** 
- [√] **任务** - 勉强可用
- [√] **朋友** - 简单聊天与指令
- [√] **忘却之庭 & 虚构叙事 & 末日幻影** 
- [√] **异相仲裁**
- [√] **成就** 

- [ ] **更多**  - Coming soon...

## 🙌鸣谢

- Weedwacker 
- [SqlSugar](https://github.com/donet5/SqlSugar) 
- [LunarCore](https://github.com/Melledy/LunarCore) 
- [DanhengServer-OpenSource](https://github.com/EggLinks/DanhengServer-OpenSource)
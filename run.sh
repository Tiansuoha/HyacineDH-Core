#!/bin/bash

#定义变量

#定义函数
wait_press(){
echo "按任意键以继续"
read -n 1 -s
}

download(){
curl -L -o $1 --connect-timeout 10 https://github.com/DBKAHHK/HyacineDH-Core/releases/download/$2/$1 ||
curl -L -o $1 https://gh-proxy.org/github.com/DBKAHHK/HyacineDH-Core/releases/download/$2/$1;
[ ! -f $1 ] && echo "下载失败，请检查网络" && exit 1
unzip -o $1 && rm -rf $1;
}

termux_set(){
    clear
    echo "欢迎使用该设置脚本~"

    echo "正在换源"
    echo "有(y/n)的选y"
    echo "有Default的直接按回车"
    wait_press 
    sed -i 's@^\(deb.*stable main\)$@#\1\ndeb https://mirrors.cernet.edu.cn/termux/apt/termux-main stable main@' $PREFIX/etc/apt/sources.list
    apt update && apt upgrade -y
    pkg install -y proot-distro 
    clear
    echo "正在安装ubuntu"
    echo "请自行检查网络设置"
    wait_press
    proot-distro install ubuntu
    touch ~/termux_finished

    clear
    echo "正在对ubuntu进行设置"
    echo "还要进行一次换源，注意同前"
    proot-distro login ubuntu --termux-home -- bash -c "$(declare -f ubuntu_set download); ubuntu_set"

    exit 0
}

ubuntu_set(){
    cn=$(lsb_release -cs)
    echo "
Types: deb
URIs: https://mirrors.cernet.edu.cn/ubuntu-ports
Suites: ${cn} ${cn}-updates ${cn}-backports
Components: main restricted universe multiverse
Signed-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg

Types: deb
URIs: http://ports.ubuntu.com/ubuntu-ports/
Suites: ${cn}-security
Components: main restricted universe multiverse
Signed-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg
    " | tee /etc/apt/sources.list.d/ubuntu.sources
    apt update && apt upgrade -y;
    apt install -y dotnet9 curl unzip;
    mkdir -p DH && cd DH;
    download "linux-arm64.zip" "1.3.0"
    mkdir -p Resources && cd Resources;
    download "Resources.zip" "1.1.0"
    cd ../;
    chmod 0777 -R ./;
    cd;
    rm -rf ~/termux_finished
    touch ~/ubuntu_finished
    echo '设置完成，请重启termux~';
}

starting_set(){
    export DOTNET_GCHeapHardLimit=1C0000000;
    cd ~/DH; 
    chmod +x HyacineCoreServer; 
    ./HyacineCoreServer
}

if [[ -f ~/termux_finished ]];then
    echo "检测到ubuntu已安装完成"
    echo "检测到配置未完成，是否继续？(y/n)(默认:n)"
    read -r answer
    if [[ $answer =~ ^[Yy] ]]; then
        proot-distro login ubuntu --termux-home -- bash -c "$(declare -f ubuntu_set download); ubuntu_set"
        echo "设置结束"
        exit 0
    fi 
fi

if [[ -f ~/ubuntu_finished ]]; then
    proot-distro login ubuntu --termux-home -- bash -c "$(declare -f starting_set); starting_set"
    exit 0
fi

clear
echo "未检测到设置记录，重新设置"
termux_set
exit 0

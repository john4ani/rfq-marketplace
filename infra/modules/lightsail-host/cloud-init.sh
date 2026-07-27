#!/usr/bin/env bash
set -Eeuo pipefail

export DEBIAN_FRONTEND=noninteractive

apt-get update
apt-get install --yes ca-certificates curl gnupg jq unattended-upgrades

install -m 0755 -d /etc/apt/keyrings
curl --fail --silent --show-error --location https://download.docker.com/linux/ubuntu/gpg \
  | gpg --dearmor --output /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg

. /etc/os-release
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
  > /etc/apt/sources.list.d/docker.list

apt-get update
apt-get install --yes docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

cat >/etc/docker/daemon.json <<'JSON'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "5"
  },
  "live-restore": true
}
JSON

systemctl enable --now docker
systemctl restart docker
usermod -aG docker ubuntu

install -d -m 0750 -o ubuntu -g ubuntu \
  /opt/rfq-marketplace \
  /opt/rfq-marketplace/releases \
  /opt/rfq-marketplace/backups
install -d -m 0700 -o 1654 -g 1654 /opt/rfq-marketplace/shared/data-protection-keys

if [[ ! -f /swapfile ]]; then
  fallocate -l 1G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo "/swapfile none swap sw 0 0" >> /etc/fstab
fi

cat >/etc/sysctl.d/99-rfq-marketplace.conf <<'SYSCTL'
vm.swappiness=10
net.ipv4.tcp_syncookies=1
SYSCTL
sysctl --system

dpkg-reconfigure --frontend=noninteractive unattended-upgrades

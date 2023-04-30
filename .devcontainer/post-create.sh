#!/bin/zsh
set -e

curl -fsSL https://get.pulumi.com | sh

if ! command -v k9s &> /dev/null
then
    curl -sS https://webinstall.dev/k9s | bash
else
    echo "k9s has already been installed."
fi
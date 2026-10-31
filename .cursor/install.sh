#!/usr/bin/env bash
# Cloud Agent Build install. Safe to re-run: skips the SDK download when
# /usr/share/dotnet is already .NET 8 with the Windows desktop targets.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

# Default Linux lookup path. App hosts (including `dotnet test`) search here
# for libhostfxr; a copy only on PATH is not enough.
install_dir="/usr/share/dotnet"

dotnet_major() {
  local version
  version="$(dotnet --version 2>/dev/null)" || return 1
  printf '%s' "${version%%.*}"
}

sdk_ready() {
  local sdk_root="$1"
  [[ -x "${sdk_root}/dotnet" ]] || return 1
  local version
  version="$("${sdk_root}/dotnet" --version 2>/dev/null)" || return 1
  [[ "${version%%.*}" == "8" ]] || return 1
  local targets
  targets="$(compgen -G "${sdk_root}/sdk/"*"/Sdks/Microsoft.NET.Sdk.WindowsDesktop/targets/Microsoft.NET.Sdk.WindowsDesktop.targets" || true)"
  [[ -n "$targets" ]]
}

if sdk_ready "$install_dir"; then
  echo "dotnet $("${install_dir}/dotnet" --version) already installed in ${install_dir}; skipping SDK install"
else
  echo "Installing .NET 8 SDK into ${install_dir}"
  tmp="$(mktemp)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tmp"
  # Ubuntu's dotnet-sdk-8.0 package omits Microsoft.NET.Sdk.WindowsDesktop.
  sudo bash "$tmp" --channel 8.0 --install-dir "$install_dir"
  rm -f "$tmp"
fi

# Login shells do not load ~/.bashrc. /usr/local/bin is on the default PATH.
sudo ln -sfn "${install_dir}/dotnet" /usr/local/bin/dotnet
hash -r

if [[ "$(dotnet_major || true)" != "8" ]]; then
  echo "dotnet must be major version 8 before restore (got: $(dotnet --version 2>/dev/null || echo 'unavailable'))" >&2
  exit 1
fi

dotnet restore UmaParser.sln

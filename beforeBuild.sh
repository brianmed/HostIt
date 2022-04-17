#!/bin/bash

if [ -e "/etc/os-release" ]; then
    DIR="$(dirname "$(readlink -f "$0")")"
else
    DIR="$(dirname "$(greadlink -f "$0")")"
fi

cd "$DIR"

cat <<EOT > WhenBuilt.cs
using System;

namespace HostIt
{
    public static class WhenBuilt
    {
        public static DateTime ItWas = DateTime.Parse("$(date '+%FT%T')");
    }
}
EOT

exit 0

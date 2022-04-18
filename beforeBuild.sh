#!/bin/bash

if [ -e "/etc/os-release" ]; then
    DIR="$(dirname "$(readlink -f "$0")")"
else
    DIR="$(dirname "$(greadlink -f "$0")")"
fi

cd "$DIR"

NOW=$(date '+%FT%T')

cat <<EOT > WhenBuilt.txt
HostIt Copyright (C) 2022 Brian Medley"
HostIt Version [Apache License 2.0]: $NOW"
HostIt https://github.com/brianmed/HostIt"
EOT

cat <<EOT > WhenBuilt.cs
using System;

namespace HostIt
{
    public static class WhenBuilt
    {
        public static DateTime ItWas = DateTime.Parse("$NOW");
    }
}
EOT

exit 0

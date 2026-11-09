#!/usr/bin/env bash
# Copies this case's fixtures/ into the run's working directory.
set -eu
here=$(dirname "$(printf '%s' "$0" | tr '\' '/')")
cp -R "$here/fixtures/." .

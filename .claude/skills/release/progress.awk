# Filters release.ps1 and watch-release.ps1 output down to the lines worth relaying.
# Run with gawk: the record separator splits on \r as well as \n, because the watcher redraws its
# status line with \r and no newline.
BEGIN { RS = "[\r\n]" }
{ gsub(/\x1b\[[0-9;]*[A-Za-z]/, "") }
/^[[:space:]]*$/ { next }
# The watcher's in-place status line: two spaces, a step name, an elapsed time, "...".
/^  .*\.\.\.$/ { next }
# dotnet build and test chatter.
/ -> [A-Za-z]:\\/ { next }
/Determining projects to restore|All projects are up-to-date|^Test run for |^A total of / { next }
{ print; fflush() }

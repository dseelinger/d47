# worker

The Cloudflare Worker backing donations. Deploy with `wrangler deploy` from this directory. Its
`node --test` suite is deliberately not part of `dotnet test`, and nothing in the .NET build
references it.

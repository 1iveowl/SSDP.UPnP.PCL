dotnet restore -f netcoreapp2.1
dotnet publish -c Release -r linux-arm -f netcoreapp2.1 --source "https://api.nuget.org/v3/index.json"

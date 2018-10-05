FROM microsoft/dotnet:2.1-sdk AS build

WORKDIR /app

# copy csproj and restore as distinct layers
COPY main/*.sln ./main/
COPY test/Console.NETCore.Test/*.csproj ./test/Console.NETCore.Test/
COPY test/Console.Device.NETCore/*.csproj ./test/Console.Device.NETCore/
COPY test/Unit/SSDP.Device.xUnit/*.csproj ./test//Unit/SSDP.Device.xUnit/
COPY main/SSDP.UPnP.Netstandard/*.csproj ./main/SSDP.UPnP.Netstandard/
COPY interfaces/ISSDP.UPnP.Netstandard/*.csproj ./interfaces/ISSDP.UPnP.Netstandard/

WORKDIR /app/main
RUN dotnet restore

WORKDIR /app
# copy everything else and build app
COPY . .


WORKDIR /app/test/Console.NETCore.Test/
RUN dotnet build


FROM build AS publish
WORKDIR /app/test/Console.NETCore.Test/
RUN dotnet publish -c Release -o out --source "https://api.nuget.org/v3/index.json"  

EXPOSE 1900 8321 8322

FROM microsoft/dotnet:2.1-runtime AS runtime
WORKDIR /app
COPY --from=publish /app/test/Console.NETCore.Test/out/ ./

ENTRYPOINT ["dotnet", "Console.NETCore.Test.dll"]  
CMD [""]
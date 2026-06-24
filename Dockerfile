# Runtime image: docker build -t messharp . && docker run --rm -v "$PWD":/code messharp /code text csharp
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/MessSharp/MessSharp.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "/app/messharp.dll"]

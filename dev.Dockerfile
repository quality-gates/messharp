# Development image: docker build -f dev.Dockerfile -t messharp-dev . && docker run --rm -it -v "$PWD":/workspace messharp-dev
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /workspace
COPY . .
CMD ["dotnet", "test"]

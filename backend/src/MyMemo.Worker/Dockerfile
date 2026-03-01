FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["global.json", "./"]
COPY ["src/MyMemo.Shared/MyMemo.Shared.csproj", "src/MyMemo.Shared/"]
COPY ["src/MyMemo.Worker/MyMemo.Worker.csproj", "src/MyMemo.Worker/"]
RUN dotnet restore "src/MyMemo.Worker/MyMemo.Worker.csproj"
COPY src/ src/
RUN dotnet publish "src/MyMemo.Worker/MyMemo.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyMemo.Worker.dll"]

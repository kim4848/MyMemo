FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["global.json", "./"]
COPY ["src/MyMemo.Shared/MyMemo.Shared.csproj", "src/MyMemo.Shared/"]
COPY ["src/MyMemo.Worker/MyMemo.Worker.csproj", "src/MyMemo.Worker/"]
RUN dotnet restore "src/MyMemo.Worker/MyMemo.Worker.csproj" -r linux-musl-x64
COPY src/ src/
RUN dotnet publish "src/MyMemo.Worker/MyMemo.Worker.csproj" -c Release -r linux-musl-x64 --self-contained -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
RUN apk add --no-cache ffmpeg icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["./MyMemo.Worker"]

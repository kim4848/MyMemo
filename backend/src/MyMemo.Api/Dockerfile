FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["global.json", "./"]
COPY ["src/MyMemo.Shared/MyMemo.Shared.csproj", "src/MyMemo.Shared/"]
COPY ["src/MyMemo.Api/MyMemo.Api.csproj", "src/MyMemo.Api/"]
RUN dotnet restore "src/MyMemo.Api/MyMemo.Api.csproj" -r linux-musl-x64
COPY src/ src/
RUN dotnet publish "src/MyMemo.Api/MyMemo.Api.csproj" -c Release -r linux-musl-x64 --self-contained -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
RUN apk add --no-cache icu-libs
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["./MyMemo.Api"]

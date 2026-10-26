FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyası: paket geri yüklemesi katman önbelleğinden gelsin.
COPY EventPlatform.Web/EventPlatform.Web.csproj EventPlatform.Web/
RUN dotnet restore EventPlatform.Web/EventPlatform.Web.csproj

COPY EventPlatform.Web/ EventPlatform.Web/
RUN dotnet publish EventPlatform.Web/EventPlatform.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Render varsayılan olarak 10000 numaralı portu bekler.
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

COPY --from=build /app/publish .

# Kök olmayan kullanıcı (aspnet imajında hazır gelir).
USER $APP_UID
ENTRYPOINT ["dotnet", "EventPlatform.Web.dll"]
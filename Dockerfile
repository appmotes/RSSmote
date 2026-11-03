# The server with the browser app beside it (A24). The library lives in the /data volume.
#   docker build -t rssmote .
#   docker run -d -p 8080:8080 -e RSSMOTE_KEY=… -v rssmote:/data rssmote
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN dotnet workload install wasm-tools
WORKDIR /src
COPY . .
RUN dotnet publish src/RSSmote.Browser/RSSmote.Browser.csproj -c Release -o /out/web \
 && dotnet publish src/RSSmote.Server/RSSmote.Server.csproj -c Release -o /out/server \
 && cp -R /out/web/wwwroot /out/server/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/server .
RUN mkdir /data && chown app /data
ENV RSSMOTE_DATA_DIR=/data \
    ASPNETCORE_URLS=http://+:8080
VOLUME /data
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "RSSmote.Server.dll"]

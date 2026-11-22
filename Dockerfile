# Builds the API host, which also serves the Blazor WebAssembly client.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Mingle.slnx ./
COPY src/Mingle.Engine/Mingle.Engine.csproj src/Mingle.Engine/
COPY src/Mingle.Client/Mingle.Client.csproj src/Mingle.Client/
COPY src/Mingle.DAL/Mingle.DAL.csproj src/Mingle.DAL/
COPY src/Mingle.Api/Mingle.Api.csproj src/Mingle.Api/
RUN dotnet restore src/Mingle.Api/Mingle.Api.csproj
COPY src/ src/
RUN dotnet publish src/Mingle.Api/Mingle.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Mingle.Api.dll"]

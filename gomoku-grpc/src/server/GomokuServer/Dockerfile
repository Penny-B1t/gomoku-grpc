FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY proto/ proto/

COPY src/server/GomokuServer/GomokuServer.csproj src/server/GomokuServer/
RUN dotnet restore src/server/GomokuServer/GomokuServer.csproj

COPY src/server/GomokuServer/ src/server/GomokuServer/

WORKDIR /app/src/server/GomokuServer
RUN dotnet publish -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /publish .
EXPOSE 5224
ENV ASPNETCORE_URLS=http://+:5224
ENTRYPOINT ["dotnet", "GomokuServer.dll"]
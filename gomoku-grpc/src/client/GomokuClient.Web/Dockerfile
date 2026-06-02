FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY proto/ proto/

COPY src/client/GomokuClient.Web/GomokuClient.Web.csproj src/client/GomokuClient.Web/
RUN dotnet restore src/client/GomokuClient.Web/GomokuClient.Web.csproj

COPY src/client/GomokuClient.Web/ src/client/GomokuClient.Web/

WORKDIR /app/src/client/GomokuClient.Web
RUN dotnet publish -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /publish .
EXPOSE 5051
ENV ASPNETCORE_URLS=http://+:5051
ENTRYPOINT ["dotnet", "GomokuClient.Web.dll"]
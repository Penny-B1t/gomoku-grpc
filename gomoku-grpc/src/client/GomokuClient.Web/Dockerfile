FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy the proto folder (relative path as expected by GomokuClient.Web.csproj)
COPY proto/ proto/

# Copy csproj and restore
COPY src/client/GomokuClient.Web/GomokuClient.Web.csproj src/client/GomokuClient.Web/
RUN dotnet restore src/client/GomokuClient.Web/GomokuClient.Web.csproj

# Copy the rest of the source code
COPY src/client/GomokuClient.Web/ src/client/GomokuClient.Web/

# Publish
WORKDIR /app/src/client/GomokuClient.Web
RUN dotnet publish -c Release -o /publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /publish .
EXPOSE 5051
ENV ASPNETCORE_URLS=http://+:5051
ENTRYPOINT ["dotnet", "GomokuClient.Web.dll"]

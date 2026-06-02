FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy the proto folder (relative path as expected by GomokuServer.csproj)
COPY proto/ proto/

# Copy csproj and restore
COPY src/server/GomokuServer/GomokuServer.csproj src/server/GomokuServer/
RUN dotnet restore src/server/GomokuServer/GomokuServer.csproj

# Copy the rest of the source code
COPY src/server/GomokuServer/ src/server/GomokuServer/

# Publish
WORKDIR /app/src/server/GomokuServer
RUN dotnet publish -c Release -o /publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /publish .
EXPOSE 5224
ENV ASPNETCORE_URLS=http://+:5224
ENTRYPOINT ["dotnet", "GomokuServer.dll"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restaura apenas os projetos necessários para cache eficiente
COPY Pim4.Server/Pim4.Server.csproj Pim4.Server/
COPY pim4.client/pim4.client.esproj pim4.client/
RUN dotnet restore Pim4.Server/Pim4.Server.csproj

# Copia o restante do código
COPY Pim4.Server Pim4.Server
COPY pim4.client pim4.client

# Publica em Release (sem UseAppHost para imagem menor)
RUN dotnet publish Pim4.Server/Pim4.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pim4.Server.dll"]

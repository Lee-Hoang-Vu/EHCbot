# =====================================================
# BUILD
# =====================================================

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore "EHCTelebot/EHCTelebot.csproj"

RUN dotnet publish "EHCTelebot/EHCTelebot.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# =====================================================
# RUNTIME
# =====================================================

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "EHCTelebot.dll"]
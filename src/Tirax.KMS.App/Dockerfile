FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /build/src
COPY src/Tirax.KMS.Core/*.csproj Tirax.KMS.Core/
COPY src/Tirax.KMS.App/*.csproj Tirax.KMS.App/
WORKDIR /build/src/Tirax.KMS.App
RUN dotnet restore

WORKDIR /build
COPY . .
WORKDIR /build/src/Tirax.KMS.App
RUN dotnet publish -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "Tirax.KMS.App.dll"]
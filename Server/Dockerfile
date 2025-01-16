#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
ARG TAG_NAME
WORKDIR /app
EXPOSE 8080
EXPOSE 443
COPY ./Server/EMRDirectTestCA.crt /etc/ssl/certs
COPY ./Server/EMRDirectTestClientSubCA.crt /etc/ssl/certs
COPY ./Server/SureFhirLabs_CA.cer /etc/ssl/certs
COPY ./Server/ngnix-proxy-TestCA.cer /etc/ssl/certs
COPY ./USERTrust-RSA-Certification-Authority.crt /etc/ssl/certs
COPY ./Sectigo-AAA.crt /etc/ssl/certs
RUN update-ca-certificates


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ENV GCPDeploy=true
ENV UseMauiEssentials=false
ARG TAG_NAME
ENV TAG_NAME_ENV=$TAG_NAME

COPY ["nuget.config", "."]
COPY ["Server/UdapEd.Server.csproj", "Server/"]
COPY ["Client/UdapEd.Client.csproj", "Client/"]
COPY ["Shared/UdapEd.Shared.csproj", "Shared/"]

RUN dotnet restore -p:TargetFramework=net8.0 "Shared/UdapEd.Shared.csproj"
RUN dotnet restore -p:TargetFramework=net8.0 "Server/UdapEd.Server.csproj"
RUN dotnet restore -p:TargetFramework=net8.0 "Client/UdapEd.Client.csproj" 
COPY . .

RUN dotnet build "Server/UdapEd.Server.csproj" -p:TargetFramework=net8.0 -c Release --no-restore -o /app/build 


FROM build AS publish
ARG TAG_NAME
ENV UseMauiEssentials=false
RUN dotnet publish "Server/UdapEd.Server.csproj" -p:TargetFramework=net8.0  -c Release --no-restore -o /app/publish /p:UseAppHost=false 

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://*:8080
ENTRYPOINT ["dotnet", "UdapEd.Server.dll"]

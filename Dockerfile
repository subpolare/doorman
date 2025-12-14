FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ClubDoorman.sln ./
COPY ClubDoorman/ClubDoorman.csproj ClubDoorman/
RUN dotnet restore ClubDoorman/ClubDoorman.csproj
COPY . .
RUN dotnet publish ClubDoorman/ClubDoorman.csproj \
    -c Release -o /app/out \
    -r linux-x64 --self-contained true

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
ENV DOTNET_System_Globalization_Invariant=1
ENV DOTNET_EnableDiagnostics=0
WORKDIR /app
COPY --from=build /app/out/ .
ENTRYPOINT ["./ClubDoorman"]

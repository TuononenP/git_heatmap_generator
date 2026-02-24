# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy and restore as distinct layers
COPY ["src/git_heatmap_generator/git_heatmap_generator.csproj", "src/git_heatmap_generator/"]
RUN dotnet restore "src/git_heatmap_generator/git_heatmap_generator.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "src/git_heatmap_generator/git_heatmap_generator.csproj" -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

# Install font dependencies for ImageSharp/SixLabors.Fonts
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    fonts-dejavu \
    && rm -rf /var/lib/apt/lists/*

# The tool needs access to a local git repository. 
# You should mount your repo to /repo and your output folder to /out
# Usage: docker run -v /path/to/repo:/repo -v /path/to/output:/out git-heatmap-generator [args]

ENTRYPOINT ["dotnet", "git_heatmap_generator.dll"]

#!/bin/bash
# Optimized .NET 8 setup script with parallel execution, smart caching, and EF integration
set -e
# Configuration
DOTNET_INSTALL_DIR="$HOME/.dotnet"
APP_DIR="/app" # Default application directory in Jules VM
DOTNET_VERSION="8.0"
DOTNET_CHANNEL="8.0"
# Global tools to install
GLOBAL_TOOLS=(
    "dotnet-ef"
    "dotnet-aspnet-codegenerator"
    "dotnet-dev-certs"
)
# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}
# Function to add PATH to bashrc only if not present
add_to_path_if_missing() {
    local path_entry="$1"
    local bashrc_path="$HOME/.bashrc"
    
    if ! grep -qF "$path_entry" "$bashrc_path" 2>/dev/null; then
        echo "$path_entry" >> "$bashrc_path"
        echo "Added to PATH: $path_entry"
    fi
}
# Function to setup PATH for current session
setup_current_path() {
    export PATH="$PATH:$DOTNET_INSTALL_DIR:$HOME/.dotnet/tools"
    export DOTNET_ROOT="$DOTNET_INSTALL_DIR"
}
echo "Starting optimized .NET 8 setup..."
# Function to setup project dependencies
setup_project_deps() {
    if [ -d "$APP_DIR" ]; then
        (
            cd "$APP_DIR"
            
            # Look for solution files first, then project files
            if find . -maxdepth 2 -name "*.sln" | head -1 | read -r sln_file; then
                echo "Found solution file: $sln_file"
                echo "Restoring solution packages..."
                dotnet restore "$sln_file" --verbosity quiet
                echo "✅ Solution packages restored"
            elif find . -maxdepth 2 -name "*.csproj" -o -name "*.fsproj" -o -name "*.vbproj" | head -1 | read -r proj_file; then
                echo "Found project file: $proj_file"
                echo "Restoring project packages..."
                dotnet restore "$proj_file" --verbosity quiet
                echo "✅ Project packages restored"
            else
                echo "ℹ️ No .NET project or solution files found"
            fi
        ) &
        echo $! # Return PID of background process
    else
        echo "0" # Indicate no project directory
    fi
}
# Function to install global tools
install_global_tools() {
    echo "Installing .NET global tools..."
    local pids=()
    
    for tool in "${GLOBAL_TOOLS[@]}"; do
        if ! dotnet tool list -g | grep -q "$tool"; then
            echo "Installing $tool..."
            dotnet tool install --global "$tool" --verbosity quiet &
            pids+=($!)
        else
            echo "✅ $tool already installed"
        fi
    done
    
    # Wait for all tool installations to complete
    for pid in "${pids[@]}"; do
        wait "$pid" 2>/dev/null || true # Suppress 'No such process' errors if a PID is already done
    done
    
    echo "✅ Global tools installation complete"
}
# Check if .NET is already installed and working
if command_exists dotnet && dotnet --version >/dev/null 2>&1; then
    INSTALLED_VERSION=$(dotnet --version | cut -d. -f1-2)
    if [ "$INSTALLED_VERSION" = "$DOTNET_VERSION" ]; then
        echo "✅ .NET $DOTNET_VERSION already installed and working"
        setup_current_path
        
        # Install global tools in parallel with project setup
        install_global_tools &
        TOOLS_PID=$!
        
        # Setup project dependencies
        if [ -d "$APP_DIR" ]; then
            echo "Setting up project dependencies..."
            PROJECT_PID=$(setup_project_deps)
            
            # Wait for project dependencies (only if they have valid PIDs)
            if [ "$PROJECT_PID" -ne 0 ]; then
                wait $PROJECT_PID 2>/dev/null || true
            fi
        fi
        
        # Wait for global tools installation
        wait $TOOLS_PID 2>/dev/null || true
        echo "✅ All dependencies setup complete"
        echo "✅ Script execution complete (.NET was already set up)!"
        exit 0
    else
        echo "Different .NET version found ($INSTALLED_VERSION), will install .NET $DOTNET_VERSION"
    fi
fi
# --- Parallel Setup: System Dependencies & .NET Installation ---
echo "Setting up system dependencies and .NET in parallel..."
# Background job 1: Install system dependencies
{
    echo "Installing system dependencies..."
    
    # Set non-interactive mode for apt
    export DEBIAN_FRONTEND=noninteractive
    
    # Update package lists
    sudo -E apt-get update -qq
    
    # Install required packages for .NET with non-interactive flags
    sudo -E apt-get install -y -qq --no-install-recommends \
        -o Dpkg::Options::="--force-confdef" \
        -o Dpkg::Options::="--force-confold" \
        wget \
        ca-certificates \
        apt-transport-https \
        software-properties-common \
        libc6 \
        libgcc1 \
        libgssapi-krb5-2 \
        libicu-dev \
        libssl-dev \
        libstdc++6 \
        zlib1g \
        curl \
        git || {
            echo "Warning: Some packages may have failed to install, continuing..."
        }
    
    echo "✅ System dependencies installation complete"
} &
DEPS_PID=$!
# Detect architecture and prepare for .NET download
ARCH=$(dpkg --print-architecture)
case $ARCH in
    amd64) DOTNET_ARCH="x64" ;;
    arm64) DOTNET_ARCH="arm64" ;;
    armhf) DOTNET_ARCH="arm" ;;
    *) echo "Unsupported architecture: $ARCH"; exit 1 ;;
esac
# Create temporary directory for download (in main shell)
TEMP_DIR=$(mktemp -d)
# Background job 2: Download .NET install script
{
    echo "Downloading .NET $DOTNET_VERSION..."
    
    cd "$TEMP_DIR"
    
    # Download .NET install script
    wget -q https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
    chmod +x dotnet-install.sh
    
    echo "✅ .NET download preparation complete"
} &
DOWNLOAD_PID=$!
# Wait for both background jobs
wait $DEPS_PID
wait $DOWNLOAD_PID
# --- Install .NET ---
echo "Installing .NET $DOTNET_VERSION..."
# Go to temp directory where we downloaded the installer
cd "$TEMP_DIR"
# Install .NET SDK
./dotnet-install.sh --channel $DOTNET_CHANNEL --install-dir "$DOTNET_INSTALL_DIR" --architecture "$DOTNET_ARCH" --verbose
# Cleanup temp directory
cd /
rm -rf "$TEMP_DIR"
# --- Setup PATH ---
echo "Configuring PATH..."
add_to_path_if_missing "export PATH=\"\$PATH:$DOTNET_INSTALL_DIR\""
add_to_path_if_missing "export PATH=\"\$PATH:\$HOME/.dotnet/tools\""
add_to_path_if_missing "export DOTNET_ROOT=\"$DOTNET_INSTALL_DIR\""
setup_current_path
# Verify .NET installation
if ! command_exists dotnet; then
    echo "❌ Error: .NET command not found after installation"
    exit 1
fi
echo "✅ .NET $(dotnet --version) installed successfully"
# --- .NET Configuration ---
echo "Configuring .NET..."
# Configure .NET telemetry and first-run experience
{
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    export DOTNET_NOLOGO=1
    
    # Add to bashrc
    add_to_path_if_missing "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    add_to_path_if_missing "export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
    add_to_path_if_missing "export DOTNET_NOLOGO=1"
} &
# Warm up .NET (first run initialization)
{
    echo "Warming up .NET CLI..."
    dotnet --info >/dev/null 2>&1
    echo "✅ .NET CLI warmed up"
} &
# Install global tools in parallel
install_global_tools &
TOOLS_PID=$!
# If app directory exists, prepare for dependency installation
if [ -d "$APP_DIR" ]; then
    echo "App directory found, will restore packages after .NET setup..."
    NEED_DEPS=true
else
    echo "No app directory found at $APP_DIR"
    NEED_DEPS=false
fi
# Wait for configuration tasks to complete
wait
# Install project dependencies if needed
if [ "$NEED_DEPS" = true ]; then
    echo "Installing project dependencies..."
    
    # Start project restore
    PROJECT_PID=$(setup_project_deps)
    
    # Wait for project restore to complete (only if it started)
    if [ "$PROJECT_PID" -ne 0 ]; then
        wait $PROJECT_PID 2>/dev/null || true
    fi
    
    # If Entity Framework projects are found, ensure database tools are ready
    if [ -d "$APP_DIR" ]; then
        (
            cd "$APP_DIR"
            if find . -name "*.csproj" -exec grep -l "Microsoft.EntityFrameworkCore" {} \; | head -1 >/dev/null 2>&1; then
                echo "Entity Framework detected, verifying EF tools..."
                if command_exists dotnet-ef; then
                    echo "✅ Entity Framework tools ready"
                else
                    echo "Installing Entity Framework tools..."
                    dotnet tool install --global dotnet-ef --verbosity quiet
                fi
            fi
        )
    fi
fi
# Wait for global tools installation
wait $TOOLS_PID 2>/dev/null || true
echo "✅ Optimized .NET 8 setup complete!"
echo ""
echo "Installed components:"
echo "  - .NET $(dotnet --version)"
echo "  - Entity Framework Core tools"
echo "  - ASP.NET Core code generator"
echo "  - Development certificates tool"
echo ""
echo "Run 'source ~/.bashrc' or restart your shell to use .NET in new sessions."
echo ""
echo "Quick start commands:"
echo "  dotnet --version        # Check .NET version"
echo "  dotnet new --list       # List project templates"
echo "  dotnet ef --version     # Check EF tools version"

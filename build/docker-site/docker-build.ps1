<# PowerShell helper to build Docker targets from the unified Dockerfile #>
param(
  [string]$Tag = 'articulate:chiseled'
)

# Use buildx if available
try {
  docker buildx version > $null 2>&1
  $useBuildx = $true
} catch {
  $useBuildx = $false
}

if ($useBuildx) {
  Write-Host "Building with docker buildx..."
  docker buildx build --progress=plain --file Dockerfile --target chiseled --tag $Tag .
} else {
  Write-Host "Building with docker build..."
  docker build --file Dockerfile --target chiseled --tag $Tag .
}

Write-Host "Built $Tag (target chiseled)"

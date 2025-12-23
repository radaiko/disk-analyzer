# GitHub Actions Workflows

## Docker Build and Push (`docker-build-push.yml`)

This workflow automatically builds and publishes Docker images to GitHub Container Registry (ghcr.io).

### Triggers
- Push to `dev` branch
- Push to `main` branch

### What it does
1. Checks out the repository code
2. Sets up Docker Buildx for advanced build features
3. Authenticates with GitHub Container Registry
4. Builds the Docker image using the Dockerfile in the repository root
5. Tags the image with:
   - Branch name (e.g., `ghcr.io/radaiko/disk-analyzer:dev` or `ghcr.io/radaiko/disk-analyzer:main`)
   - `latest` tag for the main branch
   - Commit SHA with branch prefix (e.g., `ghcr.io/radaiko/disk-analyzer:main-sha-abc123`)
6. Pushes the image to GitHub Packages
7. Uses GitHub Actions cache to speed up subsequent builds

### Using the Published Images

After the workflow runs, you can pull the images using:

```bash
# Pull the latest image (from main branch)
docker pull ghcr.io/radaiko/disk-analyzer:latest

# Pull from dev branch
docker pull ghcr.io/radaiko/disk-analyzer:dev

# Pull from main branch
docker pull ghcr.io/radaiko/disk-analyzer:main
```

### Permissions

The workflow requires the following permissions:
- `contents: read` - To read the repository code
- `packages: write` - To push images to GitHub Container Registry

These are automatically granted via the `GITHUB_TOKEN` secret.

### Image Visibility

By default, the package inherits the repository's visibility settings. For private repositories, you may need to configure package visibility in the repository settings under "Packages" or authenticate when pulling images:

```bash
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin
docker pull ghcr.io/radaiko/disk-analyzer:latest
```

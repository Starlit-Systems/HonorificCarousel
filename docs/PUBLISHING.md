# Publishing Notes

These notes are for Starlit Systems maintenance, not in-game usage.

## Account Isolation

Use the `starlit-systems` GitHub account only for this project. Keep this repository's Git identity local to this folder:

```powershell
git config user.name "Starlit Systems"
git config user.email "YOUR_STARLIT_SYSTEMS_NOREPLY_EMAIL"
```

If you use SSH, create a separate key and host alias for this account:

```powershell
ssh-keygen -t ed25519 -C "Starlit Systems" -f "$env:USERPROFILE\.ssh\id_ed25519_starlit_systems"
```

Add this to your SSH config:

```sshconfig
Host github-starlit
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_starlit_systems
  IdentitiesOnly yes
```

Then use this remote URL:

```powershell
git remote add origin git@github-starlit:starlit-systems/HonorificCarousel.git
```

## First Publish

Create a public repository named `HonorificCarousel` under the `starlit-systems` account, then run:

```powershell
git init
git config user.name "Starlit Systems"
git config user.email "YOUR_STARLIT_SYSTEMS_NOREPLY_EMAIL"
git add .
git commit -m "Initial Honorific Carousel plugin"
git branch -M main
git remote add origin git@github-starlit:starlit-systems/HonorificCarousel.git
git push -u origin main
git tag v0.0.0.1
git push origin v0.0.0.1
```

The tag push creates a GitHub release with `HonorificCarousel.zip`.

## Tester Install URL

Give testers this custom repository URL:

```text
https://raw.githubusercontent.com/starlit-systems/HonorificCarousel/main/pluginmaster.json
```

## Version Updates

For each release:

1. Update `<Version>` in `HonorificCarousel/HonorificCarousel.csproj`.
2. Update `AssemblyVersion` in `pluginmaster.json`.
3. Commit and push to `main`.
4. Tag the same version, for example `v0.0.0.2`.
5. Push the tag.

## License Note

This project was built from GoatCorp's SamplePlugin template, which uses `AGPL-3.0-or-later`. Keep the license unless you have a clear legal reason to change it.

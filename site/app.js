const owner = "Simonsbs";
const repo = "signalweave";
const apiUrl = `https://api.github.com/repos/${owner}/${repo}/releases?per_page=30`;

const productConfig = {
  modern: {
    prefix: "modern-v",
    empty: "No Modern release found yet."
  },
  classic: {
    prefix: "classic-v",
    empty: "No Classic release found yet."
  }
};

function formatPublishedAt(value) {
  try {
    return new Intl.DateTimeFormat("en", { dateStyle: "medium" }).format(new Date(value));
  } catch {
    return value;
  }
}

function describeAsset(assetName) {
  if (assetName.includes("linux-x64")) return "Linux x64";
  if (assetName.includes("win-x64")) return "Windows x64";
  if (assetName.includes("osx-arm64")) return "macOS Apple Silicon";
  if (assetName.includes("osx-x64")) return "macOS Intel";
  return assetName;
}

function renderRelease(product, release) {
  const metaHost = document.querySelector(`.release-meta[data-product="${product}"]`);
  const downloadsHost = document.querySelector(`.downloads[data-product="${product}"]`);
  if (!metaHost || !downloadsHost) return;

  if (!release) {
    metaHost.innerHTML = `<p class="error">${productConfig[product].empty}</p>`;
    downloadsHost.innerHTML = "";
    return;
  }

  metaHost.innerHTML = `
    <p class="release-line"><strong>${release.name || release.tag_name}</strong></p>
    <p class="release-line">Tag: <code>${release.tag_name}</code></p>
    <p class="release-line">Published: ${formatPublishedAt(release.published_at)}</p>
    <p class="release-line"><a href="${release.html_url}" target="_blank" rel="noreferrer">Open release notes</a></p>
  `;

  const assets = [...release.assets].sort((left, right) => left.name.localeCompare(right.name));
  if (assets.length === 0) {
    downloadsHost.innerHTML = `<p class="error">Release found but no assets were attached.</p>`;
    return;
  }

  downloadsHost.innerHTML = assets.map((asset) => `
    <a class="download-link" href="${asset.browser_download_url}">
      <span>${describeAsset(asset.name)}</span>
      <span class="download-meta">${(asset.size / (1024 * 1024)).toFixed(1)} MB</span>
    </a>
  `).join("");
}

async function loadReleases() {
  try {
    const response = await fetch(apiUrl, {
      headers: {
        Accept: "application/vnd.github+json"
      }
    });

    if (!response.ok) {
      throw new Error(`GitHub API returned ${response.status}`);
    }

    const releases = await response.json();
    for (const [product, config] of Object.entries(productConfig)) {
      const release = releases.find((entry) => entry.tag_name.startsWith(config.prefix));
      renderRelease(product, release);
    }
  } catch (error) {
    for (const product of Object.keys(productConfig)) {
      const metaHost = document.querySelector(`.release-meta[data-product="${product}"]`);
      const downloadsHost = document.querySelector(`.downloads[data-product="${product}"]`);
      if (metaHost) {
        metaHost.innerHTML = `<p class="error">Failed to load release data. Use the GitHub releases page instead.</p>`;
      }
      if (downloadsHost) {
        downloadsHost.innerHTML = `
          <a class="download-link" href="https://github.com/${owner}/${repo}/releases" target="_blank" rel="noreferrer">
            <span>Open GitHub releases</span>
            <span class="download-meta">Fallback</span>
          </a>
        `;
      }
    }
    console.error(error);
  }
}

loadReleases();

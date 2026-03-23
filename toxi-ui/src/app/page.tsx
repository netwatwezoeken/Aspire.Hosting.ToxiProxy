import { ProxyCard } from "./ProxyCard";

interface Toxic {
  name: string;
  type: string;
  stream: string;
  toxicity: number;
  attributes: Record<string, any>;
}

interface Proxy {
  name: string;
  listen: string;
  upstream: string;
  enabled: boolean;
  toxics: Toxic[];
}

export default async function Home() {
  let proxies: Record<string, Proxy> = {};
  let error = null;

  try {
    const apiUrl = process.env.TOXIPROXY_URL || "http://localhost:8474";
    const response = await fetch(apiUrl + "/proxies", {
      cache: "no-store",
    });
    if (!response.ok) {
      throw new Error(`Failed to fetch proxies: ${response.statusText}`);
    }
    proxies = await response.json();
  } catch (e) {
    error = e instanceof Error ? e.message : "An unknown error occurred";
  }

  const proxyList = Object.values(proxies);

  return (
    <div className="min-h-screen bg-zinc-50 p-8 font-sans dark:bg-black">
      <main className="mx-auto max-w-4xl">
        <header className="mb-12">
          <h1 className="text-4xl font-bold tracking-tight text-black dark:text-zinc-50">
            Toxi Proxies
          </h1>
          <p className="mt-2 text-zinc-600 dark:text-zinc-400">
            A list of all active proxies and their configurations.
          </p>
        </header>

        {error ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 dark:border-red-900/50 dark:bg-red-900/20">
            <p className="text-red-800 dark:text-red-400">Error: {error}</p>
          </div>
        ) : proxyList.length === 0 ? (
          <div className="rounded-lg border border-zinc-200 bg-white p-12 text-center dark:border-zinc-800 dark:bg-zinc-900">
            <p className="text-zinc-600 dark:text-zinc-400">No proxies found.</p>
          </div>
        ) : (
          <div className="grid gap-6">
            {proxyList.map((proxy) => (
              <ProxyCard key={proxy.name} proxy={proxy} />
            ))}
          </div>
        )}
      </main>
    </div>
  );
}

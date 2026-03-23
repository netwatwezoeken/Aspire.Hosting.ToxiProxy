"use client";

import { useState } from "react";

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

export function ProxyCard({ proxy }: { proxy: Proxy }) {
  const [isExpanded, setIsExpanded] = useState(true);

  return (
    <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm transition-shadow hover:shadow-md dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between border-b border-zinc-100 bg-zinc-50/50 px-6 py-4 dark:border-zinc-800 dark:bg-zinc-800/50">
        <h2 className="text-xl font-semibold text-black dark:text-zinc-50">
          {proxy.name}
        </h2>
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
            proxy.enabled
              ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400"
              : "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400"
          }`}
        >
          {proxy.enabled ? "Enabled" : "Disabled"}
        </span>
      </div>
      <div className="px-6 py-4">
        <dl className="grid grid-cols-1 gap-x-4 gap-y-4 sm:grid-cols-2">
          <div>
            <dt className="text-sm font-medium text-zinc-500 dark:text-zinc-400">
              Listen
            </dt>
            <dd className="mt-1 font-mono text-sm text-black dark:text-zinc-50">
              {proxy.listen}
            </dd>
          </div>
          <div>
            <dt className="text-sm font-medium text-zinc-500 dark:text-zinc-400">
              Upstream
            </dt>
            <dd className="mt-1 font-mono text-sm text-black dark:text-zinc-50">
              {proxy.upstream}
            </dd>
          </div>
          <div className="sm:col-span-2">
            <button
              onClick={() => setIsExpanded(!isExpanded)}
              className="flex w-full items-center justify-between text-left focus:outline-none"
            >
              <dt className="text-sm font-medium text-zinc-500 dark:text-zinc-400">
                Toxics ({proxy.toxics.length})
              </dt>
              {proxy.toxics.length > 0 && (
                <span className="text-zinc-400">
                  {isExpanded ? (
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      width="20"
                      height="20"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    >
                      <path d="m18 15-6-6-6 6" />
                    </svg>
                  ) : (
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      width="20"
                      height="20"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    >
                      <path d="m6 9 6 6 6-6" />
                    </svg>
                  )}
                </span>
              )}
            </button>
            {isExpanded && (
              <dd className="mt-2 space-y-3">
                {proxy.toxics.length === 0 ? (
                  <span className="italic text-sm text-zinc-400">
                    No toxics configured
                  </span>
                ) : (
                  proxy.toxics.map((toxic, index) => (
                    <div
                      key={index}
                      className="rounded-lg border border-zinc-100 bg-zinc-50/50 p-3 dark:border-zinc-800 dark:bg-zinc-800/30"
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="font-semibold text-zinc-900 dark:text-zinc-100">
                          {toxic.name}
                        </span>
                        <div className="flex gap-2">
                          <span className="rounded bg-blue-100 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-blue-800 dark:bg-blue-900/30 dark:text-blue-400">
                            {toxic.type}
                          </span>
                          <span className="rounded bg-zinc-200 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-zinc-700 dark:bg-zinc-700 dark:text-zinc-300">
                            {toxic.stream}
                          </span>
                        </div>
                      </div>
                      <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-zinc-600 dark:text-zinc-400">
                        <div>
                          <span className="font-medium">Toxicity:</span>{" "}
                          {(toxic.toxicity * 100).toFixed(0)}%
                        </div>
                        <div className="col-span-2 flex flex-wrap gap-x-3 gap-y-1">
                          {Object.entries(toxic.attributes).map(
                            ([key, value]) => (
                              <div key={key}>
                                <span className="font-medium text-zinc-500 dark:text-zinc-500">
                                  {key}:
                                </span>{" "}
                                {value}
                              </div>
                            ),
                          )}
                        </div>
                      </div>
                    </div>
                  ))
                )}
              </dd>
            )}
            {!isExpanded && proxy.toxics.length > 0 && (
              <dd className="mt-1">
                <p className="text-xs text-zinc-400 italic">Click to view {proxy.toxics.length} toxics</p>
              </dd>
            )}
          </div>
        </dl>
      </div>
    </div>
  );
}

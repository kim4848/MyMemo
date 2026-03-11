import { useState, useCallback, useRef, useEffect } from 'react';
import { api } from '../api/client';
import type { Infographic } from '../types';

interface Props {
  sessionId: string;
}

function formatDuration(ms: number): string {
  return ms >= 1000 ? `${(ms / 1000).toFixed(1)}s` : `${ms}ms`;
}

export default function InfographicViewer({ sessionId }: Props) {
  const [infographic, setInfographic] = useState<Infographic | null>(null);
  const [generating, setGenerating] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const pollRef = useRef<ReturnType<typeof setInterval>>(undefined);

  useEffect(() => {
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, []);

  const startPolling = useCallback(() => {
    if (pollRef.current) clearInterval(pollRef.current);
    pollRef.current = setInterval(async () => {
      try {
        const result = await api.infographics.get(sessionId);
        setInfographic(result);
        setGenerating(false);
        setShowModal(true);
        clearInterval(pollRef.current);
        pollRef.current = undefined;
      } catch {
        // Not ready yet — keep polling
      }
    }, 3000);
  }, [sessionId]);

  const handleGenerate = useCallback(async () => {
    setGenerating(true);
    setError(null);
    try {
      await api.infographics.generate(sessionId);
      startPolling();
    } catch (e) {
      setError((e as Error).message);
      setGenerating(false);
    }
  }, [sessionId, startPolling]);

  const handleView = useCallback(async () => {
    if (infographic) {
      setShowModal(true);
      return;
    }
    setLoading(true);
    try {
      const result = await api.infographics.get(sessionId);
      setInfographic(result);
      setShowModal(true);
    } catch {
      // No existing infographic — generate one
      await handleGenerate();
    } finally {
      setLoading(false);
    }
  }, [sessionId, infographic, handleGenerate]);

  const handleDownloadPng = useCallback(() => {
    if (!infographic) return;
    const byteString = atob(infographic.imageContent);
    const bytes = new Uint8Array(byteString.length);
    for (let i = 0; i < byteString.length; i++) {
      bytes[i] = byteString.charCodeAt(i);
    }
    const blob = new Blob([bytes], { type: 'image/png' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `infographic-${sessionId}.png`;
    a.click();
    URL.revokeObjectURL(url);
  }, [infographic, sessionId]);

  return (
    <>
      <button
        onClick={handleView}
        disabled={generating || loading}
        className="rounded-lg border border-navy-600 bg-navy-700 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-accent hover:text-accent disabled:opacity-40 disabled:cursor-not-allowed sm:py-1"
      >
        {generating ? (
          <span className="flex items-center gap-1.5">
            <span className="h-3 w-3 animate-spin rounded-full border border-gray-600 border-t-accent" />
            Generating...
          </span>
        ) : loading ? (
          <span className="flex items-center gap-1.5">
            <span className="h-3 w-3 animate-spin rounded-full border border-gray-600 border-t-accent" />
            Loading...
          </span>
        ) : (
          'Infographic'
        )}
      </button>

      {error && (
        <p className="text-xs text-red-400 mt-1">{error}</p>
      )}

      {showModal && infographic && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
          <div className="relative flex max-h-[90vh] w-full max-w-3xl flex-col rounded-2xl border border-navy-700 bg-navy-900 shadow-2xl">
            <div className="flex items-center justify-between border-b border-navy-700 px-5 py-3">
              <h3 className="text-sm font-semibold text-white">Infographic</h3>
              <div className="flex items-center gap-2">
                <button
                  onClick={handleDownloadPng}
                  className="rounded-lg border border-navy-600 bg-navy-700 px-3 py-1 text-xs font-medium text-gray-300 transition-colors hover:border-accent hover:text-accent"
                >
                  PNG
                </button>
                <button
                  onClick={handleGenerate}
                  disabled={generating}
                  className="rounded-lg border border-navy-600 bg-navy-700 px-3 py-1 text-xs font-medium text-gray-300 transition-colors hover:border-accent hover:text-accent disabled:opacity-40"
                >
                  {generating ? 'Regenerating...' : 'Regenerate'}
                </button>
                <button
                  onClick={() => setShowModal(false)}
                  className="ml-1 rounded-lg p-1 text-gray-400 transition-colors hover:text-white"
                >
                  <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>
            <div className="overflow-auto p-5">
              <img
                src={`data:image/png;base64,${infographic.imageContent}`}
                alt="Meeting infographic"
                className="mx-auto h-auto max-w-full rounded-lg"
              />
            </div>
            <div className="border-t border-navy-700 px-5 py-2">
              <p className="text-xs text-gray-600">
                Model: {infographic.modelUsed}
                {infographic.promptTokens != null && (
                  <> &middot; Tokens: {infographic.promptTokens} + {infographic.completionTokens ?? 0}</>
                )}
                {infographic.generationDurationMs != null && (
                  <> &middot; Generation: {formatDuration(infographic.generationDurationMs)}</>
                )}
              </p>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

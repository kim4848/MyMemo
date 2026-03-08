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
  const [error, setError] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const svgContainerRef = useRef<HTMLDivElement>(null);
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
    try {
      const result = await api.infographics.get(sessionId);
      setInfographic(result);
      setShowModal(true);
    } catch {
      // No existing infographic — generate one
      await handleGenerate();
    }
  }, [sessionId, infographic, handleGenerate]);

  const handleDownloadPng = useCallback(() => {
    if (!infographic) return;
    const svgString = infographic.svgContent;
    const blob = new Blob([svgString], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const img = new Image();
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = img.naturalWidth * 2;
      canvas.height = img.naturalHeight * 2;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      ctx.scale(2, 2);
      ctx.drawImage(img, 0, 0);
      canvas.toBlob((pngBlob) => {
        if (!pngBlob) return;
        const pngUrl = URL.createObjectURL(pngBlob);
        const a = document.createElement('a');
        a.href = pngUrl;
        a.download = `infographic-${sessionId}.png`;
        a.click();
        URL.revokeObjectURL(pngUrl);
      }, 'image/png');
      URL.revokeObjectURL(url);
    };
    img.src = url;
  }, [infographic, sessionId]);

  const handleDownloadSvg = useCallback(() => {
    if (!infographic) return;
    const blob = new Blob([infographic.svgContent], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `infographic-${sessionId}.svg`;
    a.click();
    URL.revokeObjectURL(url);
  }, [infographic, sessionId]);

  return (
    <>
      <button
        onClick={handleView}
        disabled={generating}
        className="rounded-lg border border-navy-600 bg-navy-700 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-accent hover:text-accent disabled:opacity-40 disabled:cursor-not-allowed sm:py-1"
      >
        {generating ? (
          <span className="flex items-center gap-1.5">
            <span className="h-3 w-3 animate-spin rounded-full border border-gray-600 border-t-accent" />
            Generating...
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
                  onClick={handleDownloadSvg}
                  className="rounded-lg border border-navy-600 bg-navy-700 px-3 py-1 text-xs font-medium text-gray-300 transition-colors hover:border-accent hover:text-accent"
                >
                  SVG
                </button>
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
              <div
                ref={svgContainerRef}
                className="mx-auto w-full [&>svg]:mx-auto [&>svg]:h-auto [&>svg]:max-w-full"
                dangerouslySetInnerHTML={{ __html: infographic.svgContent }}
              />
            </div>
            <div className="border-t border-navy-700 px-5 py-2">
              <p className="text-xs text-gray-600">
                Model: {infographic.modelUsed} &middot; Tokens: {infographic.promptTokens ?? 0} + {infographic.completionTokens ?? 0}
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

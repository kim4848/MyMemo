import { useState, useEffect } from 'react';
import type { Memo, Session } from '../types';
import { useOneNote } from '../hooks/useOneNote';
import type { Notebook, NoteSection } from '../hooks/useOneNote';

interface Props {
  memo: Memo;
  session: Session;
  onClose: () => void;
}

type Step = 'notebooks' | 'sections' | 'success';

export default function OneNotePickerModal({ memo, session, onClose }: Props) {
  const { getNotebooks, getSections, createPage } = useOneNote();

  const [step, setStep] = useState<Step>('notebooks');
  const [notebooks, setNotebooks] = useState<Notebook[]>([]);
  const [sections, setSections] = useState<NoteSection[]>([]);
  const [selectedNotebook, setSelectedNotebook] = useState<Notebook | null>(null);
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getNotebooks()
      .then(setNotebooks)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  async function handleNotebookSelect(notebook: Notebook) {
    setSelectedNotebook(notebook);
    setLoading(true);
    setError(null);
    try {
      const secs = await getSections(notebook.id);
      setSections(secs);
      setStep('sections');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }

  async function handleSectionSelect(section: NoteSection) {
    setSending(true);
    setError(null);
    try {
      await createPage(section.id, session, memo);
      setStep('success');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
      <div className="w-full max-w-md rounded-2xl border border-navy-700 bg-navy-900 shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-navy-700 px-6 py-4">
          <h2 className="text-base font-semibold text-white">
            {step === 'notebooks' && 'Select Notebook'}
            {step === 'sections' && (
              <span className="flex items-center gap-2">
                <button
                  onClick={() => { setStep('notebooks'); setError(null); }}
                  className="text-gray-500 transition-colors hover:text-accent"
                >
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" />
                  </svg>
                </button>
                Select Section
              </span>
            )}
            {step === 'success' && 'Sent to OneNote'}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-500 transition-colors hover:text-white"
            aria-label="Close"
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div className="px-6 py-4">
          {step === 'success' && (
            <div className="py-4 text-center">
              <div className="mb-3 text-4xl">✓</div>
              <p className="text-gray-300">
                Page created in{' '}
                <span className="font-medium text-white">{selectedNotebook?.displayName}</span>
              </p>
              <button
                onClick={onClose}
                className="mt-5 rounded-lg bg-accent px-5 py-2 text-sm font-medium text-navy-900 transition-opacity hover:opacity-90"
              >
                Done
              </button>
            </div>
          )}

          {step !== 'success' && loading && (
            <div className="flex items-center justify-center py-8">
              <div className="h-5 w-5 animate-spin rounded-full border-2 border-gray-600 border-t-accent" />
            </div>
          )}

          {step !== 'success' && !loading && error && (
            <div className="rounded-lg border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400">
              {error}
            </div>
          )}

          {step === 'notebooks' && !loading && !error && (
            <ul className="divide-y divide-navy-700">
              {notebooks.length === 0 && (
                <li className="py-4 text-center text-sm text-gray-500">No notebooks found</li>
              )}
              {notebooks.map((nb) => (
                <li key={nb.id}>
                  <button
                    onClick={() => handleNotebookSelect(nb)}
                    className="flex w-full items-center gap-3 px-1 py-3 text-left text-sm text-gray-200 transition-colors hover:text-accent"
                  >
                    <svg className="h-4 w-4 shrink-0 text-gray-500" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25" />
                    </svg>
                    {nb.displayName}
                    <svg className="ml-auto h-4 w-4 text-gray-600" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
                    </svg>
                  </button>
                </li>
              ))}
            </ul>
          )}

          {step === 'sections' && !loading && !error && (
            <ul className="divide-y divide-navy-700">
              {sections.length === 0 && (
                <li className="py-4 text-center text-sm text-gray-500">No sections found</li>
              )}
              {sections.map((sec) => (
                <li key={sec.id}>
                  <button
                    disabled={sending}
                    onClick={() => handleSectionSelect(sec)}
                    className="flex w-full items-center gap-3 px-1 py-3 text-left text-sm text-gray-200 transition-colors hover:text-accent disabled:opacity-50"
                  >
                    <svg className="h-4 w-4 shrink-0 text-gray-500" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                    </svg>
                    {sec.displayName}
                    {sending
                      ? <div className="ml-auto h-4 w-4 animate-spin rounded-full border-2 border-gray-600 border-t-accent" />
                      : (
                        <svg className="ml-auto h-4 w-4 text-gray-600" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
                        </svg>
                      )}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

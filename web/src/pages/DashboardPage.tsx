import { useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useSessionsStore } from '../stores/sessions';
import SessionCard from '../components/SessionCard';
import TagFilter from '../components/TagFilter';
import PageHeader from '../components/PageHeader';
import { SkeletonCard } from '../components/Skeleton';

export default function DashboardPage() {
  const { sessions, loading, error, selectedTagIds, fetchSessions, fetchTags, deleteSession } =
    useSessionsStore();

  useEffect(() => {
    fetchSessions();
    fetchTags();
  }, [fetchSessions, fetchTags]);

  const filteredSessions = useMemo(() => {
    if (selectedTagIds.length === 0) return sessions;
    return sessions.filter((s) =>
      selectedTagIds.every((tagId) => s.tags.some((t) => t.id === tagId)),
    );
  }, [sessions, selectedTagIds]);

  if (loading) {
    return (
      <div className="animate-[fadeInUp_0.3s_ease-out]">
        <PageHeader title="Sessions" />
        <div className="space-y-3">
          <SkeletonCard />
          <SkeletonCard />
          <SkeletonCard />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="py-8 text-center text-danger">{error}</div>
    );
  }

  const completed = sessions.filter((s) => s.status === 'completed').length;
  const processing = sessions.filter((s) => s.status === 'processing').length;
  const subtitleParts = [`${sessions.length} session${sessions.length !== 1 ? 's' : ''}`];
  if (completed > 0) subtitleParts.push(`${completed} completed`);
  if (processing > 0) subtitleParts.push(`${processing} processing`);

  return (
    <div className="animate-[fadeInUp_0.3s_ease-out]">
      <PageHeader
        title="Sessions"
        subtitle={sessions.length > 0 ? subtitleParts.join(' · ') : undefined}
        actions={
          <Link
            to="/record"
            className="hidden sm:inline-flex items-center gap-2 rounded-lg bg-accent px-4 py-2.5 text-sm font-medium text-white shadow-sm hover:bg-accent-hover transition-colors"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z" />
            </svg>
            New Recording
          </Link>
        }
      />

      <TagFilter />

      {filteredSessions.length === 0 ? (
        <div className="rounded-xl border border-border bg-bg-card p-12 text-center shadow-sm animate-[fadeInUp_0.3s_ease-out]">
          <svg className="mx-auto h-20 w-20 text-text-muted/40" fill="none" viewBox="0 0 80 80" strokeWidth={1}>
            <circle cx="40" cy="36" r="12" stroke="currentColor" strokeWidth="2" />
            <path d="M40 48v6m-6 0h12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            <path d="M28 36a12 12 0 0 1 24 0" stroke="currentColor" strokeWidth="2" fill="none" />
            <path d="M22 36c0-2.5.5-5 1.5-7" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.5" />
            <path d="M58 36c0-2.5-.5-5-1.5-7" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.5" />
            <path d="M18 36c0-4 1-8 3-11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.3" />
            <path d="M62 36c0-4-1-8-3-11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.3" />
          </svg>
          {sessions.length === 0 ? (
            <>
              <h3 className="font-heading text-lg font-semibold text-text-primary mt-4">No recordings yet</h3>
              <p className="mt-1.5 text-sm text-text-secondary max-w-xs mx-auto">
                Record your first meeting to get started with AI-powered transcription.
              </p>
              <Link
                to="/record"
                className="mt-5 inline-flex items-center gap-2 rounded-lg bg-accent px-5 py-2.5 font-medium text-white hover:bg-accent-hover transition-colors"
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z" />
                </svg>
                Start Recording
              </Link>
            </>
          ) : (
            <p className="mt-3 text-text-secondary">No sessions match selected tags</p>
          )}
        </div>
      ) : (
        <div className="animate-[fadeInUp_0.3s_ease-out] space-y-3">
          {filteredSessions.map((session) => (
            <SessionCard
              key={session.id}
              session={session}
              onDelete={deleteSession}
            />
          ))}
        </div>
      )}
    </div>
  );
}

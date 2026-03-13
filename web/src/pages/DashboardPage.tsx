import { useEffect, useMemo } from 'react';
import { useSessionsStore } from '../stores/sessions';
import SessionCard from '../components/SessionCard';
import TagFilter from '../components/TagFilter';

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
    return <div className="py-8 text-center text-gray-500">Loading...</div>;
  }

  if (error) {
    return (
      <div className="py-8 text-center text-red-400">{error}</div>
    );
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-white">Sessions</h1>
        <div className="mt-1 flex items-center gap-2">
          <span className="inline-block h-1 w-1 rounded-full bg-accent" />
          <span className="h-px w-10 bg-accent" />
        </div>
      </div>

      <TagFilter />

      {filteredSessions.length === 0 ? (
        <div className="rounded-xl border border-navy-700 bg-navy-800 p-12 text-center">
          <svg className="mx-auto h-12 w-12 text-gray-600" fill="none" viewBox="0 0 24 24" strokeWidth={1} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z" />
          </svg>
          <p className="mt-3 text-gray-500">
            {sessions.length === 0 ? 'No sessions yet' : 'No sessions match selected tags'}
          </p>
          {sessions.length === 0 && (
            <p className="mt-1 text-sm text-gray-600">Start a new recording to get going</p>
          )}
        </div>
      ) : (
        <div className="space-y-3">
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

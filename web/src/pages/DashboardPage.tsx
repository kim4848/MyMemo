import { useEffect } from 'react';
import { useSessionsStore } from '../stores/sessions';
import SessionCard from '../components/SessionCard';

export default function DashboardPage() {
  const { sessions, loading, error, fetchSessions, deleteSession } =
    useSessionsStore();

  useEffect(() => {
    fetchSessions();
  }, [fetchSessions]);

  if (loading) {
    return <div className="py-8 text-center text-gray-500">Loading...</div>;
  }

  if (error) {
    return (
      <div className="py-8 text-center text-red-500">{error}</div>
    );
  }

  if (sessions.length === 0) {
    return (
      <div className="py-12 text-center">
        <p className="text-gray-500">No sessions yet</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <h1 className="text-lg font-semibold text-gray-900">Sessions</h1>
      {sessions.map((session) => (
        <SessionCard
          key={session.id}
          session={session}
          onDelete={deleteSession}
        />
      ))}
    </div>
  );
}

interface SkeletonProps {
  className?: string;
}

export default function Skeleton({ className = '' }: SkeletonProps) {
  return (
    <div
      className={`animate-[shimmer_1.5s_linear_infinite] bg-gradient-to-r from-bg-hover via-border/50 to-bg-hover bg-[length:200%_100%] rounded ${className}`}
    />
  );
}

export function SkeletonCard() {
  return (
    <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm space-y-3">
      <Skeleton className="h-4 w-3/4" />
      <Skeleton className="h-3 w-1/2" />
      <Skeleton className="h-3 w-1/3" />
    </div>
  );
}

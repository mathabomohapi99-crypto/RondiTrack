import Link from 'next/link';

// A friendly "nothing here" box. It is not an error and not a 403.
export function EmptyState({ message, linkHref, linkText }: { message: string; linkHref?: string; linkText?: string }) {
  return (
    <div className="rounded-lg border border-dashed p-8 text-center">
      <p>{message}</p>
      {linkHref && linkText && (
        <Link href={linkHref} className="mt-3 inline-block underline">
          {linkText}
        </Link>
      )}
    </div>
  );
}
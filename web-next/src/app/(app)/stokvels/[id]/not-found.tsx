import Link from 'next/link';

// The same page for "not a member", "does not exist" and "bad id". It never says which one.
export default function StokvelNotFound() {
  return (
    <div className="rounded border p-6">
      <h1 className="text-xl font-bold">We couldn&apos;t find that stokvel</h1>
      <p className="mt-2 text-sm">Check the link, or go back to your stokvels.</p>
      <Link href="/stokvels" className="mt-4 inline-block underline">
        Back to my stokvels
      </Link>
    </div>
  );
}
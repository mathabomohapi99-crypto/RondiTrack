// CHANGED 7.2: mobile-first page. Base classes are the phone layout; md: only adds a second column.
import { ContributionList } from '@/components/ContributionList'
import { StokvelCard } from '@/components/StokvelCard'
import { ThemeToggle } from '@/components/ThemeToggle'
import { contributions, cycles, home } from '@/data/sample'

function App() {
  return (
    <main className="mx-auto flex max-w-5xl flex-col gap-6 p-4">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl font-bold">RondiTrack</h1>
        <ThemeToggle />
      </header>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl font-semibold">Lerato&apos;s stokvels</h2>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {/* key is the stokvel's database id, never the array index */}
          {home.map((entry) => (
            <StokvelCard key={entry.stokvelId} {...entry} />
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl font-semibold">Contributions by cycle</h2>
        <ContributionList cycles={cycles} contributions={contributions} />
      </section>
    </main>
  )
}

export default App
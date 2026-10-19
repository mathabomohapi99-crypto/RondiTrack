// CHANGED: stokvel cards for now. The contribution list is added after the backend fix.
import './App.css'
import { StokvelCard } from './components/StokvelCard'
import { home } from './data/sample'

function App() {
  return (
    <main>
      <h1>RondiTrack</h1>

      <h2>Lerato&apos;s stokvels</h2>
      <div className="row">
        {/* key is the stokvel's database id, never the array index */}
        {home.map((entry) => (
          <StokvelCard key={entry.stokvelId} {...entry} />
        ))}
      </div>
    </main>
  )
}

export default App
import { useEffect, useState, type ReactNode } from 'react'
import {
  AlertTriangle,
  ArrowUpRight,
  CheckCircle2,
  Clock3,
  Database,
  ExternalLink,
  FileSearch,
  RotateCcw,
  ShieldCheck,
  XCircle,
} from 'lucide-react'
import './App.css'

type InvestigationStatus =
  | 'New'
  | 'InReview'
  | 'Confirmed'
  | 'Rejected'
  | 'Escalated'
  | 'NeedsMoreEvidence'

type PersonIdentity = {
  givenName: string
  familyName: string
  birthDate: string | null
  state: string | null
}

type PaymentActivity = {
  paymentDate: string
  amount: number
}

type ScoreContribution = {
  feature: string
  weight: number
  similarity: number
  contribution: number
  explanation: string
}

type InvestigationCase = {
  caseId: string
  status: InvestigationStatus
  createdAt: string
  matcherVersion: string
  reviewerNote: string | null
  match: {
    score: number
    confidenceBand: 'Moderate' | 'High'
    hasPostDeathPayment: boolean
    postDeathPaymentAmount: number
    contributions: ScoreContribution[]
    evidence: {
      evidenceId: string
      sourceName: string
      sourceUri: string
      publishedAt: string
      retrievedAt: string
      person: PersonIdentity
      deathDate: string
      rawContentHash: string
      extractionMethod: string
    }
    beneficiary: {
      beneficiaryId: string
      person: PersonIdentity
      benefitStatus: string
      payments: PaymentActivity[]
    }
  }
}

type Dashboard = {
  evidenceRecords: number
  openCases: number
  highConfidenceCases: number
  needsReviewCases: number
  postDeathPayments: number
  lastIngestionAt: string
}

type AuditEvent = {
  eventId: string
  occurredAt: string
  actor: string
  action: string
  detail: string | null
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
})

const date = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
})

const featureLabels: Record<string, string> = {
  'family-name': 'Family name',
  'given-name-or-alias': 'Given name / alias',
  'birth-date': 'Date of birth',
  state: 'State',
  'protected-ssn-token': 'Protected SSN token',
}

function formatDate(value: string | null) {
  return value ? date.format(new Date(`${value}T00:00:00`)) : 'Not provided'
}

function fullName(person: PersonIdentity) {
  return `${person.givenName} ${person.familyName}`
}

function App() {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [cases, setCases] = useState<InvestigationCase[]>([])
  const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null)
  const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([])
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const selectedCase = cases.find((item) => item.caseId === selectedCaseId) ?? null

  async function loadData(preferredCaseId?: string) {
    try {
      setError(null)
      const [dashboardResponse, casesResponse] = await Promise.all([
        fetch('/api/dashboard'),
        fetch('/api/cases'),
      ])
      if (!dashboardResponse.ok || !casesResponse.ok) {
        throw new Error('The review service did not return a valid response.')
      }

      const nextDashboard: Dashboard = await dashboardResponse.json()
      const nextCases: InvestigationCase[] = await casesResponse.json()
      setDashboard(nextDashboard)
      setCases(nextCases)
      setSelectedCaseId((current) =>
        preferredCaseId ?? current ?? nextCases[0]?.caseId ?? null,
      )
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Unable to load cases.')
    }
  }

  useEffect(() => {
    void Promise.all([
      fetch('/api/dashboard'),
      fetch('/api/cases'),
    ]).then(async ([dashboardResponse, casesResponse]) => {
      if (!dashboardResponse.ok || !casesResponse.ok) {
        throw new Error('The review service did not return a valid response.')
      }

      const nextDashboard: Dashboard = await dashboardResponse.json()
      const nextCases: InvestigationCase[] = await casesResponse.json()
      setDashboard(nextDashboard)
      setCases(nextCases)
      setSelectedCaseId(nextCases[0]?.caseId ?? null)
    }).catch((loadError: unknown) => {
      setError(loadError instanceof Error ? loadError.message : 'Unable to load cases.')
    })
  }, [])

  useEffect(() => {
    if (!selectedCaseId) {
      return
    }

    void fetch(`/api/cases/${selectedCaseId}/audit`)
      .then((response) => response.ok ? response.json() : Promise.reject())
      .then((events: AuditEvent[]) => setAuditEvents(events))
      .catch(() => setAuditEvents([]))
  }, [selectedCaseId, cases])

  async function resetDemo() {
    setBusy(true)
    try {
      await fetch('/api/demo/reset', { method: 'POST' })
      setNote('')
      setSelectedCaseId(null)
      await loadData()
    } finally {
      setBusy(false)
    }
  }

  async function recordDecision(status: InvestigationStatus) {
    if (!selectedCase) return
    setBusy(true)
    try {
      const response = await fetch(`/api/cases/${selectedCase.caseId}/decision`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status, note: note.trim() || null }),
      })
      if (!response.ok) throw new Error('The decision could not be recorded.')
      setNote('')
      await loadData(selectedCase.caseId)
    } catch (decisionError) {
      setError(decisionError instanceof Error ? decisionError.message : 'Unable to record decision.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand-block">
          <div className="brand-mark" aria-hidden="true"><ShieldCheck size={22} /></div>
          <div>
            <div className="title-row">
              <h1>Mortality Review</h1>
              <span className="demo-badge">Synthetic demo</span>
            </div>
            <p>Office of Payment Integrity</p>
          </div>
        </div>
        <button className="button secondary" type="button" onClick={resetDemo} disabled={busy}>
          <RotateCcw size={16} /> Reset scenario
        </button>
      </header>

      {error && <div className="error-banner" role="alert"><AlertTriangle size={18} />{error}</div>}

      <section className="metrics" aria-label="Review summary">
        <Metric icon={<Database />} label="Evidence records" value={dashboard?.evidenceRecords ?? '-'} />
        <Metric icon={<FileSearch />} label="Open cases" value={dashboard?.openCases ?? '-'} emphasis />
        <Metric icon={<ShieldCheck />} label="High confidence" value={dashboard?.highConfidenceCases ?? '-'} />
        <Metric icon={<AlertTriangle />} label="Needs review" value={dashboard?.needsReviewCases ?? '-'} />
        <Metric icon={<Clock3 />} label="Potential payments" value={dashboard ? currency.format(dashboard.postDeathPayments) : '-'} />
      </section>

      <main className="workspace">
        <aside className="case-queue" aria-label="Investigation case queue">
          <div className="section-heading">
            <div>
              <span className="eyebrow">Case queue</span>
              <h2>Recent matches</h2>
            </div>
            <span className="count">{cases.length}</span>
          </div>
          <div className="queue-list">
            {cases.map((item) => (
              <button
                type="button"
                key={item.caseId}
                className={`case-row ${item.caseId === selectedCaseId ? 'selected' : ''}`}
                onClick={() => setSelectedCaseId(item.caseId)}
              >
                <div className="case-row-top">
                  <strong>{fullName(item.match.evidence.person)}</strong>
                  <span className={`confidence ${item.match.confidenceBand.toLowerCase()}`}>
                    {Math.round(item.match.score * 100)}%
                  </span>
                </div>
                <span>{item.caseId} / {item.match.evidence.person.state}</span>
                <div className="case-row-bottom">
                  <Status status={item.status} />
                  <span>{currency.format(item.match.postDeathPaymentAmount)}</span>
                </div>
              </button>
            ))}
          </div>
        </aside>

        <section className="case-detail" aria-live="polite">
          {selectedCase ? (
            <CaseDetail
              item={selectedCase}
              auditEvents={auditEvents}
              note={note}
              setNote={setNote}
              recordDecision={recordDecision}
              busy={busy}
            />
          ) : (
            <div className="empty-state"><FileSearch size={28} /><p>Select a case for review.</p></div>
          )}
        </section>
      </main>
    </div>
  )
}

function Metric({ icon, label, value, emphasis = false }: {
  icon: ReactNode
  label: string
  value: string | number
  emphasis?: boolean
}) {
  return <div className={`metric ${emphasis ? 'emphasis' : ''}`}>
    <div className="metric-icon" aria-hidden="true">{icon}</div>
    <div><span>{label}</span><strong>{value}</strong></div>
  </div>
}

function Status({ status }: { status: InvestigationStatus }) {
  const labels: Record<InvestigationStatus, string> = {
    New: 'New',
    InReview: 'In review',
    Confirmed: 'Confirmed',
    Rejected: 'Rejected',
    Escalated: 'Escalated',
    NeedsMoreEvidence: 'More evidence',
  }
  return <span className={`status status-${status.toLowerCase()}`}>{labels[status]}</span>
}

function CaseDetail({ item, auditEvents, note, setNote, recordDecision, busy }: {
  item: InvestigationCase
  auditEvents: AuditEvent[]
  note: string
  setNote: (value: string) => void
  recordDecision: (status: InvestigationStatus) => Promise<void>
  busy: boolean
}) {
  const { match } = item
  return <>
    <div className="detail-header">
      <div>
        <span className="eyebrow">{item.caseId}</span>
        <h2>{fullName(match.evidence.person)}</h2>
        <div className="header-meta"><Status status={item.status} /><span>Created {formatDate(item.createdAt.slice(0, 10))}</span></div>
      </div>
      <div className="score-block">
        <span>Match score</span>
        <strong>{Math.round(match.score * 100)}%</strong>
        <small>{match.confidenceBand} confidence</small>
      </div>
    </div>

    <div className="comparison-grid">
      <section className="record-panel evidence-panel">
        <div className="panel-label"><FileSearch size={16} /> Mortality evidence</div>
        <RecordField label="Name" value={fullName(match.evidence.person)} />
        <RecordField label="Date of birth" value={formatDate(match.evidence.person.birthDate)} />
        <RecordField label="Reported death" value={formatDate(match.evidence.deathDate)} alert />
        <RecordField label="Location" value={match.evidence.person.state ?? 'Not provided'} />
        <a className="source-link" href={match.evidence.sourceUri} target="_blank" rel="noreferrer">
          {match.evidence.sourceName}<ExternalLink size={14} />
        </a>
        <small>Retrieved {formatDate(match.evidence.retrievedAt.slice(0, 10))} / {match.evidence.extractionMethod}</small>
      </section>

      <section className="record-panel beneficiary-panel">
        <div className="panel-label"><Database size={16} /> Beneficiary projection</div>
        <RecordField label="Name" value={fullName(match.beneficiary.person)} />
        <RecordField label="Date of birth" value={formatDate(match.beneficiary.person.birthDate)} />
        <RecordField label="Benefit status" value={match.beneficiary.benefitStatus} />
        <RecordField label="Internal ID" value={match.beneficiary.beneficiaryId} />
        <div className="payment-list">
          {match.beneficiary.payments.map((payment) => (
            <div className={payment.paymentDate > match.evidence.deathDate ? 'post-death' : ''} key={payment.paymentDate}>
              <span>{formatDate(payment.paymentDate)}</span><strong>{currency.format(payment.amount)}</strong>
            </div>
          ))}
        </div>
      </section>
    </div>

    <section className="explanation-section">
      <div className="section-heading compact">
        <div><span className="eyebrow">Deterministic matcher</span><h3>Score explanation</h3></div>
        <span className="version">{item.matcherVersion}</span>
      </div>
      <div className="contribution-list">
        {match.contributions.map((contribution) => (
          <div className="contribution" key={contribution.feature}>
            <div className="contribution-label"><span>{featureLabels[contribution.feature]}</span><strong>+{Math.round(contribution.contribution * 100)}</strong></div>
            <div className="bar"><span style={{ width: `${contribution.similarity * 100}%` }} /></div>
            <small>{contribution.explanation}</small>
          </div>
        ))}
      </div>
    </section>

    <section className="decision-section">
      <div className="decision-copy">
        <span className="eyebrow">Human decision required</span>
        <h3>{currency.format(match.postDeathPaymentAmount)} paid after reported death</h3>
      </div>
      <textarea value={note} onChange={(event) => setNote(event.target.value)} placeholder="Reviewer note" aria-label="Reviewer note" />
      <div className="decision-actions">
        <button className="button confirm" type="button" disabled={busy} onClick={() => recordDecision('Confirmed')}><CheckCircle2 size={16} /> Confirm match</button>
        <button className="button danger" type="button" disabled={busy} onClick={() => recordDecision('Rejected')}><XCircle size={16} /> Reject</button>
        <button className="button secondary" type="button" disabled={busy} onClick={() => recordDecision('NeedsMoreEvidence')}><FileSearch size={16} /> More evidence</button>
        <button className="button secondary" type="button" disabled={busy} onClick={() => recordDecision('Escalated')}><ArrowUpRight size={16} /> Escalate</button>
      </div>
    </section>

    <section className="audit-section">
      <span className="eyebrow">Audit history</span>
      {auditEvents.map((event) => <div className="audit-row" key={event.eventId}>
        <span className="audit-dot" aria-hidden="true" />
        <div>
          <strong>{event.action}</strong>
          <small>{event.actor} / {new Date(event.occurredAt).toLocaleString()}</small>
          {event.detail && <p>{event.detail}</p>}
        </div>
      </div>)}
    </section>
  </>
}

function RecordField({ label, value, alert = false }: { label: string, value: string, alert?: boolean }) {
  return <div className="record-field"><span>{label}</span><strong className={alert ? 'alert-value' : ''}>{value}</strong></div>
}

export default App
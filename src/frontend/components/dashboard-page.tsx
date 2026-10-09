"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { ApiError, apiFetch } from "@/lib/api";

type UserRole = "Admin" | "Manager" | "BuildingStaff";
type PropertyType = "ApartmentBuilding" | "IndividualRoomBuilding" | "Shop";
type OccupancyStatus = "Unknown" | "Occupied" | "Vacant";

type User = { id: string; email: string; fullName: string; role: UserRole; organizationId: string };
type Organization = { id: string; name: string };
type Session = { user: User; organization: Organization };
type Unit = { id: string; name: string; isEnabled: boolean; isArchived: boolean; occupancyStatus: OccupancyStatus };
type Floor = { id: string; name: string; sortOrder: number; isArchived: boolean; units: Unit[] };
type Property = { id: string; name: string; address: string; propertyType: PropertyType; isArchived: boolean; floors: Floor[] };
type Tenant = { id: string; name: string | null; phone: string | null; onboardingStatus: "NotConnected" };
type StaffMember = { user: User; propertyIds: string[] };
type FloorDraft = { name: string; unitCount: number; unitNames: string };
type PropertyPreview = { floors: { name: string; sortOrder: number; units: { name: string; sortOrder: number }[] }[] };

const propertyTypeLabels: Record<PropertyType, string> = {
  ApartmentBuilding: "Apartment building",
  IndividualRoomBuilding: "Individual-room building",
  Shop: "Shops",
};

const roleLabels: Record<UserRole, string> = {
  Admin: "Admin",
  Manager: "Manager",
  BuildingStaff: "Building staff",
};

const statusLabels: Record<OccupancyStatus, string> = {
  Unknown: "Unknown",
  Occupied: "Occupied",
  Vacant: "Vacant",
};

const initialFloors: FloorDraft[] = [
  { name: "Floor 1", unitCount: 2, unitNames: "1A, 1B" },
  { name: "Floor 2", unitCount: 2, unitNames: "2A, 2B" },
  { name: "Floor 3", unitCount: 2, unitNames: "3A, 3B" },
  { name: "Floor 4", unitCount: 1, unitNames: "4A" },
];

export function DashboardPage() {
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  const [mode, setMode] = useState<"login" | "register">("login");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    apiFetch<Session>("/api/auth/me")
      .then(setSession)
      .catch(() => setSession(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <CenteredState label="Loading your PrimeTect workspace…" />;
  }

  if (!session) {
    return (
      <AuthScreen
        mode={mode}
        onModeChange={setMode}
        onAuthenticated={(nextSession) => {
          setError("");
          setMessage("");
          setSession(nextSession);
        }}
        onError={setError}
      />
    );
  }

  return (
    <Workspace
      session={session}
      message={message}
      error={error}
      onMessage={setMessage}
      onError={setError}
      onLogout={async () => {
        await apiFetch<void>("/api/auth/logout", { method: "POST" });
        setSession(null);
        setMode("login");
      }}
    />
  );
}

function AuthScreen({
  mode,
  onModeChange,
  onAuthenticated,
  onError,
}: {
  mode: "login" | "register";
  onModeChange: (mode: "login" | "register") => void;
  onAuthenticated: (session: Session) => void;
  onError: (message: string) => void;
}) {
  const [form, setForm] = useState({ organizationName: "", fullName: "", email: "", password: "" });
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onError("");
    setBusy(true);
    try {
      if (mode === "login") {
        await apiFetch<User>("/api/auth/login", { method: "POST", body: JSON.stringify({ email: form.email, password: form.password }) });
      } else {
        await apiFetch("/api/auth/register-owner", {
          method: "POST",
          body: JSON.stringify({ organizationName: form.organizationName, fullName: form.fullName, email: form.email, password: form.password }),
        });
      }
      const nextSession = await apiFetch<Session>("/api/auth/me");
      onAuthenticated(nextSession);
    } catch (caught) {
      onError(caught instanceof ApiError ? caught.message : "We could not complete that request.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="min-h-screen bg-sand px-6 py-8 sm:px-10 lg:px-14">
      <div className="mx-auto grid min-h-[calc(100vh-4rem)] max-w-6xl items-center gap-12 lg:grid-cols-[1fr_0.8fr]">
        <div>
          <BrandMark />
          <p className="mt-14 text-xs font-bold uppercase tracking-[0.28em] text-pine">Property operations, made clear</p>
          <h1 className="mt-5 max-w-xl text-5xl font-black leading-[0.98] tracking-[-0.07em] text-ink sm:text-7xl">A better day for every property team.</h1>
          <p className="mt-7 max-w-lg text-lg leading-8 text-ink/60">Start with a secure workspace for your organization, then shape every building floor by floor.</p>
        </div>

        <form onSubmit={submit} className="rounded-[2rem] border border-ink/10 bg-white/80 p-7 shadow-soft sm:p-9">
          <div className="flex gap-6 border-b border-ink/10 text-sm font-bold">
            <button type="button" className={`pb-4 ${mode === "login" ? "border-b-2 border-pine text-pine" : "text-ink/40"}`} onClick={() => onModeChange("login")}>Log in</button>
            <button type="button" className={`pb-4 ${mode === "register" ? "border-b-2 border-pine text-pine" : "text-ink/40"}`} onClick={() => onModeChange("register")}>Set up workspace</button>
          </div>
          <h2 className="mt-8 text-2xl font-black tracking-[-0.05em] text-ink">{mode === "login" ? "Welcome back" : "Create your organization"}</h2>
          <p className="mt-2 text-sm leading-6 text-ink/55">{mode === "login" ? "Use your PrimeTect account to continue." : "This creates the organization and its first Admin account together."}</p>

          {mode === "register" && <Field label="Organization name" value={form.organizationName} onChange={(value) => setForm({ ...form, organizationName: value })} placeholder="Northstar Property Group" />}
          {mode === "register" && <Field label="Your full name" value={form.fullName} onChange={(value) => setForm({ ...form, fullName: value })} placeholder="Amina Rahman" />}
          <Field label="Email" type="email" value={form.email} onChange={(value) => setForm({ ...form, email: value })} placeholder="you@example.com" />
          <Field label="Password" type="password" value={form.password} onChange={(value) => setForm({ ...form, password: value })} placeholder="10+ characters, mixed case and a symbol" />
          <button disabled={busy} className="mt-5 w-full rounded-2xl bg-pine px-5 py-3.5 text-sm font-bold text-white transition hover:bg-ink disabled:cursor-wait disabled:opacity-60">{busy ? "Working…" : mode === "login" ? "Log in" : "Create secure workspace"}</button>
          {mode === "register" && <p className="mt-4 text-xs leading-5 text-ink/45">Invited Managers and Building Staff join through an Admin invitation. Public setup never selects an existing organization or privileged role.</p>}
        </form>
      </div>
    </main>
  );
}

function Workspace({ session, message, error, onMessage, onError, onLogout }: { session: Session; message: string; error: string; onMessage: (message: string) => void; onError: (message: string) => void; onLogout: () => Promise<void> }) {
  const [properties, setProperties] = useState<Property[]>([]);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [busy, setBusy] = useState(false);

  async function refresh() {
    setBusy(true);
    try {
      const nextProperties = await apiFetch<Property[]>(session.user.role === "BuildingStaff" ? "/api/properties" : "/api/properties?includeArchived=true");
      const nextTenants = await apiFetch<Tenant[]>("/api/tenants");
      setProperties(nextProperties);
      setTenants(nextTenants);
      if (session.user.role !== "BuildingStaff") {
        setStaff(await apiFetch<StaffMember[]>("/api/staff"));
      }
    } catch (caught) {
      onError(caught instanceof ApiError ? caught.message : "We could not refresh your workspace.");
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, [session.user.id]);

  async function updateOccupancy(propertyId: string, unitId: string, occupancyStatus: OccupancyStatus) {
    try {
      await apiFetch(`/api/properties/${propertyId}/units/${unitId}/occupancy-status`, { method: "PUT", body: JSON.stringify({ status: occupancyStatus }) });
      onMessage("Occupancy status saved. It remains separate from tenant details.");
      await refresh();
    } catch (caught) {
      onError(caught instanceof ApiError ? caught.message : "Could not save occupancy status.");
    }
  }

  async function updateUnit(propertyId: string, unit: Unit, changes: { name?: string; isEnabled?: boolean; isArchived?: boolean }) {
    try {
      await apiFetch(`/api/properties/${propertyId}/units/${unit.id}`, { method: "PUT", body: JSON.stringify({ name: changes.name ?? unit.name, isEnabled: changes.isEnabled ?? unit.isEnabled, isArchived: changes.isArchived ?? unit.isArchived }) });
      onMessage("Unit settings saved.");
      await refresh();
    } catch (caught) {
      onError(caught instanceof ApiError ? caught.message : "Could not save unit settings.");
    }
  }

  return (
    <main className="min-h-screen bg-sand">
      <header className="border-b border-ink/10 bg-white/70 px-6 py-5 sm:px-10 lg:px-14">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-6">
          <BrandMark />
          <div className="flex items-center gap-3">
            <ApiPill />
            <div className="hidden text-right sm:block"><p className="text-sm font-bold text-ink">{session.user.fullName}</p><p className="text-xs text-ink/50">{roleLabels[session.user.role]} · {session.organization.name}</p></div>
            <button onClick={onLogout} className="rounded-full border border-ink/15 px-4 py-2 text-xs font-bold text-ink transition hover:bg-white">Log out</button>
          </div>
        </div>
      </header>

      <section className="mx-auto max-w-7xl px-6 py-10 sm:px-10 lg:px-14">
        <div className="flex flex-wrap items-end justify-between gap-6">
          <div><p className="text-xs font-bold uppercase tracking-[0.25em] text-pine">Live workspace</p><h1 className="mt-3 text-4xl font-black tracking-[-0.07em] text-ink sm:text-5xl">{session.user.role === "BuildingStaff" ? "Your assigned properties" : "Property operations"}</h1><p className="mt-3 max-w-2xl text-base leading-7 text-ink/55">Real records only. PrimeTect does not infer vacancy from missing tenant information.</p></div>
          <span className="rounded-full bg-mint px-3 py-1.5 text-xs font-bold uppercase tracking-[0.16em] text-pine">{busy ? "Refreshing" : "No demo data"}</span>
        </div>

        {message && <Notice tone="success" message={message} onClose={() => onMessage("")} />}
        {error && <Notice tone="error" message={error} onClose={() => onError("")} />}

        <div className="mt-10 grid gap-6 xl:grid-cols-[1fr_0.42fr]">
          <div className="space-y-5">
            {properties.length === 0 ? <EmptyState role={session.user.role} /> : properties.map((property) => <PropertyCard key={property.id} property={property} canEdit={session.user.role !== "BuildingStaff"} onOccupancyChange={updateOccupancy} onUnitUpdate={updateUnit} />)}
            {session.user.role !== "BuildingStaff" && <PropertySetup onSaved={refresh} onError={onError} onMessage={onMessage} />}
          </div>

          <aside className="space-y-5">
            <TenantPanel tenants={tenants} onSaved={refresh} onError={onError} />
            {session.user.role === "Admin" && <StaffPanel staff={staff} properties={properties} onSaved={refresh} onError={onError} onMessage={onMessage} />}
            {session.user.role === "Manager" && <InfoCard title="Your access"><p>Managers can work across every active property in {session.organization.name}. Staff assignments do not narrow Manager access.</p></InfoCard>}
            {session.user.role === "BuildingStaff" && <InfoCard title="Assignment boundary"><p>This view is filtered to properties assigned to your account. Unassigned properties remain unavailable.</p></InfoCard>}
          </aside>
        </div>
      </section>
    </main>
  );
}

function PropertyCard({ property, canEdit, onOccupancyChange, onUnitUpdate }: { property: Property; canEdit: boolean; onOccupancyChange: (propertyId: string, unitId: string, status: OccupancyStatus) => Promise<void>; onUnitUpdate: (propertyId: string, unit: Unit, changes: { name?: string; isEnabled?: boolean; isArchived?: boolean }) => Promise<void> }) {
  const units = property.floors.flatMap((floor) => floor.units);
  const counts = useMemo(() => units.reduce((summary, unit) => ({ ...summary, [unit.occupancyStatus]: summary[unit.occupancyStatus] + 1 }), { Occupied: 0, Vacant: 0, Unknown: 0 } as Record<OccupancyStatus, number>), [units]);

  return <article className="rounded-[2rem] border border-ink/10 bg-white/75 p-6 shadow-soft sm:p-8">
    <div className="flex flex-wrap items-start justify-between gap-4"><div><div className="flex flex-wrap items-center gap-2"><h2 className="text-2xl font-black tracking-[-0.05em] text-ink">{property.name}</h2>{property.isArchived && <span className="rounded-full bg-ink/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.14em] text-ink/50">Archived</span>}</div><p className="mt-1 text-sm text-ink/50">{propertyTypeLabels[property.propertyType]} · {property.address}</p></div><div className="text-right"><p className="text-3xl font-black tracking-[-0.07em] text-pine">{units.length}</p><p className="text-xs font-bold uppercase tracking-[0.15em] text-ink/40">units</p></div></div>
    <div className="mt-6 flex flex-wrap gap-2 text-xs font-bold"><span className="rounded-full bg-mint px-3 py-1.5 text-pine">{counts.Occupied} occupied</span><span className="rounded-full bg-sand px-3 py-1.5 text-ink/55">{counts.Vacant} vacant</span><span className="rounded-full bg-ink/5 px-3 py-1.5 text-ink/55">{counts.Unknown} unknown</span></div>
    <div className="mt-7 space-y-3">{property.floors.map((floor) => <div key={floor.id} className="rounded-2xl border border-ink/10 p-4"><div className="flex items-center justify-between gap-4"><p className="font-bold text-ink">{floor.name}</p><span className="text-xs text-ink/40">{floor.units.length} units</span></div><div className="mt-3 grid gap-2 sm:grid-cols-2">{floor.units.map((unit) => <div key={unit.id} className={`rounded-xl px-3 py-2.5 ${unit.isEnabled && !unit.isArchived ? "bg-sand/80" : "bg-ink/5"}`}><div className="flex items-center justify-between gap-2"><span className={`text-sm ${unit.isArchived ? "text-ink/35 line-through" : unit.isEnabled ? "font-semibold text-ink" : "font-semibold text-ink/45"}`}>{unit.name}</span>{canEdit ? <select aria-label={`Occupancy for ${unit.name}`} value={unit.occupancyStatus} onChange={(event) => void onOccupancyChange(property.id, unit.id, event.target.value as OccupancyStatus)} className="rounded-lg border-0 bg-white px-2 py-1 text-xs font-bold text-pine outline-none ring-1 ring-ink/10"><option value="Unknown">Unknown</option><option value="Occupied">Occupied</option><option value="Vacant">Vacant</option></select> : <span className="text-xs font-bold text-pine">{statusLabels[unit.occupancyStatus]}</span>}</div>{canEdit && <div className="mt-2 flex gap-2 text-[10px] font-bold uppercase tracking-[0.12em] text-pine"><button type="button" onClick={() => void onUnitUpdate(property.id, unit, { isEnabled: !unit.isEnabled })}>{unit.isEnabled ? "Disable" : "Enable"}</button><button type="button" onClick={() => void onUnitUpdate(property.id, unit, { isArchived: !unit.isArchived })}>{unit.isArchived ? "Unarchive" : "Archive"}</button></div>}</div>)}</div></div>)}</div>
  </article>;
}

function PropertySetup({ onSaved, onError, onMessage }: { onSaved: () => Promise<void>; onError: (message: string) => void; onMessage: (message: string) => void }) {
  const [form, setForm] = useState({ name: "", address: "", propertyType: "ApartmentBuilding" as PropertyType, floors: initialFloors });
  const [preview, setPreview] = useState<PropertyPreview | null>(null);
  const [busy, setBusy] = useState(false);

  function updateFloor(index: number, field: keyof FloorDraft, value: string) {
    setForm((current) => ({ ...current, floors: current.floors.map((floor, floorIndex) => floorIndex === index ? { ...floor, [field]: field === "unitCount" ? Math.max(0, Number(value)) : value } : floor) }));
  }

  function payload() {
    return { ...form, floors: form.floors.map((floor) => { const unitNames = floor.unitNames.split(",").map((name) => name.trim()).filter(Boolean); return { name: floor.name, unitCount: unitNames.length > 0 ? unitNames.length : floor.unitCount, unitNames }; }) };
  }

  async function showPreview() {
    setBusy(true);
    try { setPreview(await apiFetch<PropertyPreview>("/api/properties/preview", { method: "POST", body: JSON.stringify(payload()) })); onError(""); } catch (caught) { onError(caught instanceof ApiError ? caught.message : "Could not generate the unit preview."); } finally { setBusy(false); }
  }

  async function saveProperty() {
    setBusy(true);
    try { await apiFetch("/api/properties", { method: "POST", body: JSON.stringify(payload()) }); setForm({ name: "", address: "", propertyType: "ApartmentBuilding", floors: initialFloors }); setPreview(null); onMessage("Property saved as a real record."); await onSaved(); } catch (caught) { onError(caught instanceof ApiError ? caught.message : "Could not save the property."); } finally { setBusy(false); }
  }

  return <section className="rounded-[2rem] border border-pine/20 bg-mint/60 p-6 sm:p-8"><div className="flex flex-wrap items-start justify-between gap-4"><div><p className="text-xs font-bold uppercase tracking-[0.2em] text-pine">Dynamic setup</p><h2 className="mt-2 text-2xl font-black tracking-[-0.05em] text-ink">Shape a property floor by floor</h2><p className="mt-2 max-w-xl text-sm leading-6 text-ink/55">Use named units when you have them, or leave names blank to preview generated units.</p></div><span className="rounded-full bg-white/70 px-3 py-1.5 text-xs font-bold text-pine">Admin + Manager</span></div><div className="mt-6 grid gap-4 sm:grid-cols-2"><Field label="Property name" value={form.name} onChange={(value) => setForm({ ...form, name: value })} placeholder="Riverside House" /><Field label="Address" value={form.address} onChange={(value) => setForm({ ...form, address: value })} placeholder="12 River Road" /><label className="block text-sm font-bold text-ink sm:col-span-2">Property type<select value={form.propertyType} onChange={(event) => setForm({ ...form, propertyType: event.target.value as PropertyType })} className="mt-2 w-full rounded-xl border border-ink/10 bg-white px-3 py-3 font-normal outline-none focus:border-pine"><option value="ApartmentBuilding">Apartment building</option><option value="IndividualRoomBuilding">Individual-room building</option><option value="Shop">Shops</option></select></label></div><div className="mt-6 space-y-3">{form.floors.map((floor, index) => <div key={`${index}-${floor.name}`} className="grid gap-3 rounded-2xl border border-pine/10 bg-white/60 p-4 sm:grid-cols-[1fr_0.35fr_1.2fr_auto] sm:items-end"><Field label={`Floor ${index + 1} name`} value={floor.name} onChange={(value) => updateFloor(index, "name", value)} placeholder="Floor 1" /><Field label="Unit count" type="number" value={String(floor.unitCount)} onChange={(value) => updateFloor(index, "unitCount", value)} placeholder="2" /><Field label="Unit names (optional)" value={floor.unitNames} onChange={(value) => updateFloor(index, "unitNames", value)} placeholder="1A, 1B" /><button type="button" onClick={() => setForm({ ...form, floors: form.floors.filter((_, floorIndex) => floorIndex !== index) })} className="rounded-xl px-3 py-3 text-xs font-bold text-ink/45 hover:bg-white hover:text-rose-500">Remove</button></div>)}</div><div className="mt-4 flex flex-wrap gap-3"><button type="button" onClick={() => setForm({ ...form, floors: [...form.floors, { name: `Floor ${form.floors.length + 1}`, unitCount: 1, unitNames: "" }] })} className="rounded-xl border border-pine/20 bg-white/60 px-4 py-2.5 text-sm font-bold text-pine">+ Add floor</button><button type="button" disabled={busy} onClick={() => void showPreview()} className="rounded-xl bg-pine px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">{busy ? "Working…" : "Preview generated units"}</button><button type="button" disabled={busy} onClick={() => void saveProperty()} className="rounded-xl border border-pine/25 px-4 py-2.5 text-sm font-bold text-pine disabled:opacity-50">Save property</button></div>{preview && <div className="mt-6 rounded-2xl border border-pine/15 bg-white/70 p-5"><p className="text-xs font-bold uppercase tracking-[0.18em] text-pine">Generated-unit preview</p><div className="mt-4 grid gap-3 sm:grid-cols-2">{preview.floors.map((floor) => <div key={floor.sortOrder} className="rounded-xl bg-sand/70 p-3"><p className="font-bold text-ink">{floor.name}</p><p className="mt-1 text-sm text-ink/55">{floor.units.map((unit) => unit.name).join(" · ") || "No units"}</p></div>)}</div></div>}</section>;
}

function TenantPanel({ tenants, onSaved, onError }: { tenants: Tenant[]; onSaved: () => Promise<void>; onError: (message: string) => void }) {
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [busy, setBusy] = useState(false);

  async function addTenant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try { await apiFetch("/api/tenants", { method: "POST", body: JSON.stringify({ name: name || null, phone: phone || null }) }); setName(""); setPhone(""); await onSaved(); } catch (caught) { onError(caught instanceof ApiError ? caught.message : "Could not save the tenant record."); } finally { setBusy(false); }
  }

  return <InfoCard title="Tenant foundation"><form onSubmit={addTenant} className="space-y-3"><p className="text-xs leading-5 text-ink/50">Name and phone are independent. Either can be added later; onboarding is currently <strong>Not connected</strong>.</p><Field label="Name (optional)" value={name} onChange={setName} placeholder="Tenant name" /><Field label="Phone (optional)" value={phone} onChange={setPhone} placeholder="Phone number" /><button disabled={busy} className="w-full rounded-xl bg-pine px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">{busy ? "Saving…" : "Add tenant record"}</button></form><div className="mt-5 space-y-2">{tenants.length === 0 ? <p className="text-sm text-ink/45">No real tenant records yet.</p> : tenants.slice(0, 5).map((tenant) => <div key={tenant.id} className="flex items-center justify-between gap-3 rounded-xl bg-sand/70 px-3 py-2.5"><div><p className="text-sm font-bold text-ink">{tenant.name ?? "Unnamed tenant"}</p><p className="text-xs text-ink/45">{tenant.phone ?? "No phone"}</p></div><span className="text-[10px] font-bold uppercase tracking-[0.12em] text-pine">Not connected</span></div>)}</div></InfoCard>;
}

function StaffPanel({ staff, properties, onSaved, onError, onMessage }: { staff: StaffMember[]; properties: Property[]; onSaved: () => Promise<void>; onError: (message: string) => void; onMessage: (message: string) => void }) {
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<"Manager" | "BuildingStaff">("BuildingStaff");
  const [inviteToken, setInviteToken] = useState("");
  const [busy, setBusy] = useState(false);

  async function invite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try { const response = await apiFetch<{ invitationToken: string }>("/api/staff/invitations", { method: "POST", body: JSON.stringify({ email, role }) }); setInviteToken(response.invitationToken); setEmail(""); onMessage("Invitation created. Email delivery is not connected; share the one-time token securely for local setup."); } catch (caught) { onError(caught instanceof ApiError ? caught.message : "Could not create the invitation."); } finally { setBusy(false); }
  }

  async function toggleAssignment(member: StaffMember, propertyId: string) {
    const assigned = member.propertyIds.includes(propertyId); setBusy(true);
    try { await apiFetch(`/api/properties/${propertyId}/staff/${member.user.id}`, { method: assigned ? "DELETE" : "POST" }); await onSaved(); } catch (caught) { onError(caught instanceof ApiError ? caught.message : "Could not update the staff assignment."); } finally { setBusy(false); }
  }

  return <InfoCard title="Staff access"><form onSubmit={invite} className="space-y-3"><p className="text-xs leading-5 text-ink/50">Only Admins can invite Managers and Building Staff. Public registration cannot choose these roles.</p><Field label="Invite email" type="email" value={email} onChange={setEmail} placeholder="staff@example.com" /><label className="block text-sm font-bold text-ink">Role<select value={role} onChange={(event) => setRole(event.target.value as "Manager" | "BuildingStaff")} className="mt-2 w-full rounded-xl border border-ink/10 bg-white px-3 py-3 font-normal outline-none focus:border-pine"><option value="BuildingStaff">Building staff</option><option value="Manager">Manager</option></select></label><button disabled={busy} className="w-full rounded-xl bg-pine px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">{busy ? "Working…" : "Create invitation"}</button></form>{inviteToken && <div className="mt-4 rounded-xl border border-sun/50 bg-sun/15 p-3"><p className="text-[10px] font-bold uppercase tracking-[0.12em] text-ink/55">Local invitation token</p><code className="mt-2 block break-all text-xs text-ink">{inviteToken}</code><p className="mt-2 text-[11px] leading-4 text-ink/55">No email integration is claimed in this milestone.</p></div>}<div className="mt-6 space-y-4">{staff.length === 0 ? <p className="text-sm text-ink/45">No invited accounts yet.</p> : staff.map((member) => <div key={member.user.id} className="rounded-xl border border-ink/10 p-3"><div className="flex items-center justify-between gap-3"><div><p className="text-sm font-bold text-ink">{member.user.fullName}</p><p className="text-xs text-ink/45">{member.user.email} · {roleLabels[member.user.role]}</p></div></div>{member.user.role === "BuildingStaff" && <div className="mt-3 grid gap-2">{properties.filter((property) => !property.isArchived).map((property) => <label key={property.id} className="flex items-center gap-2 text-xs text-ink/65"><input type="checkbox" checked={member.propertyIds.includes(property.id)} onChange={() => void toggleAssignment(member, property.id)} />{property.name}</label>)}</div>}</div>)}</div></InfoCard>;
}

function Field({ label, value, onChange, placeholder, type = "text" }: { label: string; value: string; onChange: (value: string) => void; placeholder?: string; type?: string }) {
  return <label className="block text-sm font-bold text-ink">{label}<input required={label.includes("name") || label === "Email" || label === "Password"} type={type} value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} className="mt-2 w-full rounded-xl border border-ink/10 bg-white px-3 py-3 font-normal outline-none transition placeholder:text-ink/30 focus:border-pine focus:ring-2 focus:ring-pine/10" /></label>;
}

function BrandMark() { return <div className="flex items-center gap-3"><div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-pine text-lg font-black text-white shadow-soft">P</div><span className="text-lg font-black tracking-[-0.04em] text-ink">PrimeTect</span></div>; }

function ApiPill() { return <span className="hidden items-center gap-2 rounded-full border border-pine/10 bg-white px-3 py-1.5 text-[10px] font-bold uppercase tracking-[0.15em] text-pine sm:inline-flex"><span className="h-2 w-2 rounded-full bg-emerald-500" />API connected</span>; }

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) { return <section className="rounded-[2rem] border border-ink/10 bg-white/75 p-6 shadow-soft"><h2 className="text-lg font-black tracking-[-0.04em] text-ink">{title}</h2><div className="mt-4 text-sm leading-6 text-ink/60">{children}</div></section>; }

function Notice({ tone, message, onClose }: { tone: "success" | "error"; message: string; onClose: () => void }) { return <div className={`mt-6 flex items-center justify-between gap-4 rounded-2xl px-4 py-3 text-sm ${tone === "success" ? "bg-mint text-pine" : "bg-rose-100 text-rose-800"}`}><span>{message}</span><button onClick={onClose} className="font-bold">×</button></div>; }

function EmptyState({ role }: { role: UserRole }) { return <div className="rounded-[2rem] border border-dashed border-ink/20 bg-white/35 p-8"><p className="text-xs font-bold uppercase tracking-[0.2em] text-pine">No real records yet</p><h2 className="mt-3 text-2xl font-black tracking-[-0.05em] text-ink">{role === "BuildingStaff" ? "No properties assigned" : "Start with your first property"}</h2><p className="mt-3 max-w-lg text-sm leading-6 text-ink/55">{role === "BuildingStaff" ? "An Admin can assign properties to your account. This view never shows unassigned properties." : "Use the setup editor below to configure floors and units. Demo data is never mixed into this workspace."}</p></div>; }

function CenteredState({ label }: { label: string }) { return <main className="flex min-h-screen items-center justify-center bg-sand px-6 text-sm font-bold text-pine">{label}</main>; }

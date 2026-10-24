// Golden-hour open world (GTA-style downtown + PUBG-style container yard) as one R3F component.
// Everything is procedural (canvas textures + primitives); the only download is drei's "sunset" HDRI.
// Post chain: N8AO → Bloom → ACES tone mapping → Vignette → SMAA. The composer switches renderer tone mapping
// off (three can't tone map into render targets), so ACES Filmic runs as the <ToneMapping> effect.
import { Suspense, useLayoutEffect, useMemo, useRef } from 'react'
import * as THREE from 'three'
import { Canvas, useFrame } from '@react-three/fiber'
import { BakeShadows, Environment, OrbitControls, RoundedBox, SoftShadows } from '@react-three/drei'
import { Bloom, EffectComposer, N8AO, SMAA, ToneMapping, Vignette } from '@react-three/postprocessing'
import { ToneMappingMode } from 'postprocessing'

const SUN = new THREE.Vector3(-110, 26.4, -80).setLength(300) // ~11° golden hour; Sky, sun light and shadow camera share it
const FOG = '#c28e74'
const ROADS = [-96, -48, 0, 48, 96] // road centre lines on both axes
const BLOCKS = [-72, -24, 24, 72] // block centres between them
const CITY = 102 // half-size of the paved downtown
const TILE = [24, 56] // one facade texture tile in metres: 8 bays x 16 floors
const PARK = '24,72'
const GAS = '-72,72'
const PAINT = ['#a30f1c', '#e9e9e9', '#0c0c0f', '#f0b400', '#1d4f9c', '#8e949a', '#4a1630', '#14532d']

let seed = 20261022 // deterministic: the same city on every load
const rand = () => (seed = (seed * 16807) % 2147483647) / 2147483647
const pick = (a) => a[Math.floor(rand() * a.length)]
const int = (a, b) => a + Math.floor(rand() * (b - a + 1))

// ─── Layout. Instanced items are [x, y, z, sx, sy, sz, rotY, rotZ] ──────────
const BUILDINGS = [] // [x, y0, z, w, h, d, glass, variant, uOffset, vOffset]
const SLABS = []
for (const bx of BLOCKS) for (const bz of BLOCKS) {
  const key = `${bx},${bz}`
  if (key !== PARK) SLABS.push([bx, 0.15, bz, 36, 0.3, 36])
  if (key === PARK || key === GAS) continue
  const add = (x, y, z, w, h, d, glass) => BUILDINGS.push([x, y, z, w, h, d, glass, int(0, 3), int(0, 7) / 8, int(0, 15) / 16])
  if (Math.abs(bx) < 48 && Math.abs(bz) < 48) { // downtown: tower + setback crown
    const w = 3 * int(5, 7), d = 3 * int(5, 7), h = 3.5 * int(20, 36)
    add(bx, 0.3, bz, w, h, d, true)
    add(bx, 1.3 + h, bz, w - 6, 3.5 * int(2, 5), d - 6, true)
  } else for (const ox of [-8, 8]) for (const oz of [-8, 8]) {
    if (rand() > 0.15) add(bx + ox, 0.3, bz + oz, 3 * int(3, 4), 3.5 * int(3, 11), 3 * int(3, 4), rand() < 0.3)
  }
}
const ROOFS = BUILDINGS.map(([x, y, z, w, h, d]) => [x, y + h + 0.5, z, w + 0.6, 1, d + 0.6])
const VENTS = BUILDINGS.flatMap(([x, y, z, w, h, d]) => Array.from({ length: int(1, 3) }, () =>
  [x + (rand() - 0.5) * (w - 5), y + h + 1.6, z + (rand() - 0.5) * (d - 5), 2.2, 1.2, 1.6, rand() * 3]))
const CROWNS = BUILDINGS.filter((b) => b[1] > 1)
const MASTS = CROWNS.map(([x, y, z, , h]) => [x, y + h + 7, z, 0.25, 12, 0.25])
const BEACONS = CROWNS.map(([x, y, z, , h]) => [x, y + h + 13.3, z, 0.55])

const WHITE = [], YELLOW = [] // road paint
for (const r of ROADS) for (const s of BLOCKS) {
  for (const o of [-0.2, 0.2]) YELLOW.push([r + o, 0.04, s, 0.12, 0.02, 26], [s, 0.04, r + o, 26, 0.02, 0.12])
  for (let t = -12; t <= 12; t += 6) for (const o of [-3, 3]) WHITE.push([r + o, 0.04, s + t, 0.12, 0.02, 3], [s + t, 0.04, r + o, 3, 0.02, 0.12])
}
for (const x of ROADS) for (const z of ROADS) for (const [dx, dz] of [[0, 8.5], [0, -8.5], [8.5, 0], [-8.5, 0]]) {
  if (Math.abs(x + dx) > 96 || Math.abs(z + dz) > 96) continue // zebra crossings
  for (let k = -4.8; k < 5; k += 1.2) WHITE.push(dx ? [x + dx, 0.04, z + k, 3, 0.02, 0.6] : [x + k, 0.04, z + dz, 0.6, 0.02, 3])
}
for (const s of [-1, 1]) { // highway out of town, both directions
  for (const o of [-0.2, 0.2]) YELLOW.push([s * 351, 0.04, o, 498, 0.02, 0.12])
  for (let x = 106; x < 600; x += 9) for (const o of [-3, 3]) WHITE.push([s * x, 0.04, o, 3, 0.02, 0.12])
}

const POLES = [], ARMS = [], HEADS = [] // street lights, arm reaching over the road
for (const r of ROADS) for (const s of BLOCKS) for (const t of [-9, 9]) for (const side of [-1, 1]) {
  for (const [x, z, dx, dz] of [[r + side * 7.2, s + t, -side, 0], [s + t, r + side * 7.2, 0, -side]]) {
    if (Math.abs(x) > CITY || Math.abs(z) > CITY) continue
    const ry = Math.atan2(-dz, dx)
    POLES.push([x, 4.3, z, 0.17, 8, 0.17])
    ARMS.push([x + dx * 1.2, 8.2, z + dz * 1.2, 2.4, 0.14, 0.14, ry])
    HEADS.push([x + dx * 2.3, 8.08, z + dz * 2.3, 1, 0.12, 0.45, ry])
  }
}

const CARS = [ // [x, z, rotY, paint, y]: pumps and highway first, then street traffic
  [-79.1, 72, Math.PI / 2, '#e9e9e9', 0.3], [-64.9, 70, -Math.PI / 2, '#a30f1c', 0.3],
  [140, 4.5, 0, '#0c0c0f'], [235, -1.5, Math.PI, '#f0b400'], [-150, 1.5, 0, '#1d4f9c'], [-260, -4.5, Math.PI, '#8e949a'],
]
while (CARS.length < 28) {
  const road = pick(ROADS), along = pick(BLOCKS) + (rand() - 0.5) * 26, lane = pick([-4.5, -1.5, 1.5, 4.5])
  const [x, z, ry] = rand() < 0.5
    ? [along, road + lane, lane > 0 ? 0 : Math.PI] // east-west street, right-hand traffic
    : [road + lane, along, lane > 0 ? Math.PI / 2 : -Math.PI / 2] // north-south street
  if (CARS.every((c) => Math.hypot(c[0] - x, c[1] - z) > 7)) CARS.push([x, z, ry, pick(PAINT)])
}

const TRUNKS = [], CANOPIES = [], LEAVES = [], FRONDS = []
const tree = (x, z, y = 0, k = 0.8 + rand() * 0.6) => {
  TRUNKS.push([x, y + 1.6 * k, z, 0.35 * k, 3.2 * k, 0.35 * k])
  CANOPIES.push([x, y + 4.8 * k, z, 2.8 * k, 2.4 * k, 2.8 * k, rand() * 6])
  LEAVES.push(pick(['#3f5b27', '#4b6b2a', '#34501f', '#5d6f2c', '#2e4a26']))
}
const palm = (x, z, h = 9 + rand() * 4) => {
  TRUNKS.push([x, 0.3 + h / 2, z, 0.26, h, 0.26])
  for (let i = 0; i < 9; i++) FRONDS.push([x, 0.3 + h, z, 0.35, 1, 1, (i / 9) * Math.PI * 2 + rand() * 0.3, -1.75 - rand() * 0.4])
}
for (const s of BLOCKS) for (const t of [-14, 0, 14]) for (const side of [-1, 1]) { palm(s + t, side * 7.8); palm(side * 7.8, s + t) }
for (let i = 0; i < 16; i++) if (![5, 6, 9, 10].includes(i)) tree(24 + ((i % 4) - 1.5) * 8 + rand() * 2, 72 + (Math.floor(i / 4) - 1.5) * 8 + rand() * 2, 0.3)
for (let n = 0; n < 170;) {
  const a = rand() * Math.PI * 2, r = 112 + rand() * 320, x = Math.cos(a) * r, z = Math.sin(a) * r
  const blocked = (Math.abs(x) < 110 && Math.abs(z) < 110) || Math.abs(z) < 12 || (x > 118 && x < 245 && z > -118 && z < 6) || Math.hypot(x - 180, z - 40) < 14
  if (!blocked) { tree(x, z); n++ }
}

const CONTAINERS = [], TINTS = []
for (let row = 0; row < 5; row++) for (let i = 0; i < 7; i++) {
  if (rand() < 0.2) continue
  for (let k = 0, n = rand() < 0.35 ? 2 : 1; k < n; k++) {
    CONTAINERS.push([140 + row * 6.5, 1.4 + k * 2.6, -96 + i * 13.5, 1, 1, 1, Math.PI / 2 + (rand() - 0.5) * 0.06])
    TINTS.push(pick(['#8e2f22', '#1f4f7a', '#2f6a3b', '#c4661c', '#77797c', '#5a316b']))
  }
}
for (const z of [-80, -40]) { CONTAINERS.push([205, 6.08, z, 3.4, 4.6, 12]); TINTS.push('#a3acb2') } // warehouses: same ribbed mesh, scaled
const TOWER_LEGS = [[-3, -3], [3, -3], [-3, 3], [3, 3]].map(([x, z]) => [x, 9, z, 0.3, 18, 0.3])

const HILLS = Array.from({ length: 30 }, (_, i) => {
  const a = (i / 30) * Math.PI * 2 + rand() * 0.2, r = 700 + rand() * 300, s = 200 + rand() * 150, h = 80 + rand() * 60
  return [Math.cos(a) * r, -0.45 * h, Math.sin(a) * r, s, h, s, rand() * 3] // sunk domes: only the gentle tops show
})

// ─── Procedural assets ──────────────────────────────────────────────────────
// Box whose UVs count texture tiles, so one shared texture fits every size without stretching.
function tiledBox(w, h, d, [tw, th] = TILE, u0 = 0, v0 = 0) {
  const g = new THREE.BoxGeometry(w, h, d)
  const uv = g.attributes.uv
  for (let i = 0; i < uv.count; i++) {
    const [fw, fh] = [[d, h], [d, h], [w, d], [w, d], [w, h], [w, h]][i >> 2] // face order: ±x, ±y, ±z
    uv.setXY(i, u0 + (uv.getX(i) * fw) / tw, v0 + (uv.getY(i) * fh) / th)
  }
  return g
}

let tex // canvas textures, built once on first use (needs the DOM)
function textures() {
  if (tex) return tex
  const paint = (w, h, draw, srgb = false) => {
    const c = document.createElement('canvas')
    c.width = w
    c.height = h
    draw(c.getContext('2d'))
    const t = new THREE.CanvasTexture(c)
    t.wrapS = t.wrapT = THREE.RepeatWrapping
    t.anisotropy = 8
    if (srgb) t.colorSpace = THREE.SRGBColorSpace
    return t
  }
  const windows = (g, fill) => {
    for (let i = 0; i < 128; i++) {
      const f = fill(i)
      if (f) { g.fillStyle = f; g.fillRect((i % 8) * 64 + 9, (i >> 3) * 64 + 12, 46, 40) }
    }
  }
  const lit = Array.from({ length: 128 }, () => rand() < 0.12 && pick(['#ffcf8a', '#ffb565', '#ffe2b0']))
  tex = {
    // white wall + near-black glass. Used as colour map AND roughness map, so the glass is the glossy part.
    facade: paint(512, 1024, (g) => {
      g.fillStyle = '#fff'
      g.fillRect(0, 0, 512, 1024)
      windows(g, () => `hsl(212,12%,${rand() < 0.7 ? 3 + rand() * 6 : 14 + rand() * 16}%)`) // dark glass, some blinds
    }),
    // only the lit windows: emissive map
    glow: paint(512, 1024, (g) => {
      g.fillStyle = '#000'
      g.fillRect(0, 0, 512, 1024)
      windows(g, (i) => { g.globalAlpha = 0.45 + rand() * 0.55; return lit[i] }) // uneven interior light
    }, true),
    // aggregate grain + dark wet patches. Colour AND roughness again: the dark patches are the shiny ones.
    asphalt: paint(512, 512, (g) => {
      g.fillStyle = '#b8b8b8'
      g.fillRect(0, 0, 512, 512)
      for (let i = 0; i < 9000; i++) {
        const v = int(110, 235)
        g.fillStyle = `rgb(${v},${v},${v})`
        g.fillRect(rand() * 512, rand() * 512, 2, 2)
      }
      for (let i = 0; i < 6; i++) {
        const r = 25 + rand() * 50, x = r + rand() * (512 - 2 * r), y = r + rand() * (512 - 2 * r)
        const grad = g.createRadialGradient(x, y, 0, x, y, r)
        grad.addColorStop(0, 'rgba(20,20,20,0.6)')
        grad.addColorStop(1, 'rgba(20,20,20,0)')
        g.fillStyle = grad
        g.fillRect(x - r, y - r, 2 * r, 2 * r)
      }
    }),
    // corrugated sheet metal: bump map, 4 ribs per tile
    ribs: paint(64, 4, (g) => {
      for (let x = 0; x < 64; x++) {
        const v = Math.round(128 + 127 * Math.sin((x / 64) * Math.PI * 8))
        g.fillStyle = `rgb(${v},${v},${v})`
        g.fillRect(x, 0, 1, 4)
      }
    }),
  }
  return tex
}

// Many copies of one mesh in a single draw call.
function Instanced({ items, colors, children, ...props }) {
  const ref = useRef()
  useLayoutEffect(() => {
    const o = new THREE.Object3D(), c = new THREE.Color()
    items.forEach(([x, y, z, sx = 1, sy = sx, sz = sx, ry = 0, rz = 0], i) => {
      o.position.set(x, y, z)
      o.rotation.set(0, ry, rz)
      o.scale.set(sx, sy, sz)
      o.updateMatrix()
      ref.current.setMatrixAt(i, o.matrix)
      if (colors) ref.current.setColorAt(i, c.set(colors[i]))
    })
    ref.current.instanceMatrix.needsUpdate = true
    ref.current.computeBoundingSphere()
  }, [items, colors])
  return <instancedMesh ref={ref} args={[undefined, undefined, items.length]} {...props}>{children}</instancedMesh>
}

// ─── Scene parts ────────────────────────────────────────────────────────────
function Ground() {
  const t = textures()
  const [terrain, city, highway, asphalt] = useMemo(() => [
    tiledBox(3000, 0.1, 3000, [20, 20]),
    tiledBox(CITY * 2, 0.06, CITY * 2, [16, 16]),
    tiledBox(498, 0.06, 12, [16, 16]),
    // asphalt is a dielectric: the look is the roughness map (~0.4-0.8 grain, ~0.3 in wet patches) + a hint of metalness
    new THREE.MeshStandardMaterial({ color: '#55555b', map: t.asphalt, roughnessMap: t.asphalt, roughness: 0.9, metalness: 0.08 }),
  ], [t])
  return (
    <>
      <mesh geometry={terrain} position-y={-0.05} receiveShadow>
        <meshStandardMaterial color="#5c6436" roughness={1} metalness={0} />
      </mesh>
      <mesh geometry={city} material={asphalt} receiveShadow />
      {[-351, 351].map((x) => <mesh key={x} geometry={highway} material={asphalt} position-x={x} receiveShadow />)}
    </>
  )
}

function Streets() {
  return (
    <>
      <Instanced items={SLABS} castShadow receiveShadow><boxGeometry /><meshStandardMaterial color="#a19b90" roughness={0.92} /></Instanced>
      <Instanced items={WHITE} receiveShadow><boxGeometry /><meshStandardMaterial color="#e6e4dc" roughness={0.6} /></Instanced>
      <Instanced items={YELLOW} receiveShadow><boxGeometry /><meshStandardMaterial color="#e0aa22" roughness={0.6} /></Instanced>
      <Instanced items={POLES} castShadow><cylinderGeometry args={[1, 1, 1, 8]} /><meshStandardMaterial color="#2c2f33" metalness={0.85} roughness={0.4} /></Instanced>
      <Instanced items={ARMS} castShadow><boxGeometry /><meshStandardMaterial color="#2c2f33" metalness={0.85} roughness={0.4} /></Instanced>
      <Instanced items={HEADS}><boxGeometry /><meshBasicMaterial color={[9, 6.4, 3.4]} /></Instanced>
    </>
  )
}

function City() {
  const t = textures()
  const parts = useMemo(() => {
    const shared = { roughnessMap: t.facade, emissiveMap: t.glow, emissive: '#ffffff', emissiveIntensity: 1.3 }
    // glass towers: metallic curtain walls that mirror the sunset; stone: matte walls with glossy windows
    const glass = ['#3f6f9e', '#9a7446', '#2f7b77', '#5b6f86'].map((color) => new THREE.MeshStandardMaterial({ ...shared, color, metalness: 0.8, roughness: 0.55 }))
    const stone = ['#b7a487', '#8e4a35', '#a39f97', '#6d7681'].map((color) => new THREE.MeshStandardMaterial({ ...shared, map: t.facade, color, metalness: 0, roughness: 0.95 }))
    return BUILDINGS.map(([x, y, z, w, h, d, isGlass, v, u0, v0]) => ({
      position: [x, y + h / 2, z], geometry: tiledBox(w, h, d, TILE, u0, v0), material: (isGlass ? glass : stone)[v],
    }))
  }, [t])
  return (
    <>
      {parts.map((p, i) => <mesh key={i} {...p} castShadow receiveShadow />)}
      <Instanced items={ROOFS} castShadow receiveShadow><boxGeometry /><meshStandardMaterial color="#7b776f" roughness={0.9} /></Instanced>
      <Instanced items={VENTS} castShadow receiveShadow><boxGeometry /><meshStandardMaterial color="#b8bcc0" metalness={0.8} roughness={0.35} /></Instanced>
      <Instanced items={MASTS} castShadow><cylinderGeometry args={[1, 1, 1, 6]} /><meshStandardMaterial color="#9da3a8" metalness={0.9} roughness={0.3} /></Instanced>
      <Beacons />
    </>
  )
}

function Beacons() { // aviation lights on the towers
  const mat = useRef()
  useFrame(({ clock }) => {
    const k = Math.sin(clock.elapsedTime * 3) > 0.5 ? 14 : 0.4
    mat.current.color.setRGB(k, k * 0.04, k * 0.03)
  })
  return <Instanced items={BEACONS}><sphereGeometry args={[1, 12, 8]} /><meshBasicMaterial ref={mat} /></Instanced>
}

const WHEELS = [[1.45, 0.86], [1.45, -0.86], [-1.45, 0.86], [-1.45, -0.86]]
function Car({ car: [x, z, ry, paint, y = 0.03] }) {
  return (
    <group position={[x, y, z]} rotation-y={ry}>
      {/* metallic flake paint under a clear coat */}
      <RoundedBox args={[4.5, 0.8, 1.95]} radius={0.25} smoothness={3} position-y={0.78} castShadow receiveShadow>
        <meshPhysicalMaterial color={paint} metalness={0.65} roughness={0.32} clearcoat={1} clearcoatRoughness={0.04} />
      </RoundedBox>
      {/* tinted glass: dielectric, reflects only at grazing angles */}
      <RoundedBox args={[2.4, 0.72, 1.75]} radius={0.3} smoothness={3} position={[-0.3, 1.42, 0]} castShadow>
        <meshStandardMaterial color="#06080b" metalness={0} roughness={0.05} />
      </RoundedBox>
      {WHEELS.map(([wx, wz]) => (
        <mesh key={`${wx}:${wz}`} position={[wx, 0.42, wz]} rotation-x={Math.PI / 2} castShadow>
          <cylinderGeometry args={[0.42, 0.42, 0.3, 18]} />
          <meshStandardMaterial color="#111111" roughness={0.9} />
        </mesh>
      ))}
      <mesh position={[2.24, 0.86, 0]}><boxGeometry args={[0.06, 0.16, 1.6]} /><meshBasicMaterial color={[7, 6.5, 5.5]} /></mesh>
      <mesh position={[-2.24, 0.9, 0]}><boxGeometry args={[0.06, 0.14, 1.7]} /><meshBasicMaterial color={[6, 0.25, 0.15]} /></mesh>
    </group>
  )
}

function Vegetation() {
  const [canopy, frond] = useMemo(() => {
    const ico = new THREE.IcosahedronGeometry(1, 1), p = ico.attributes.position, v = new THREE.Vector3()
    for (let i = 0; i < p.count; i++) { // lumpy canopy; offset depends on position only, so shared corners stay welded
      v.fromBufferAttribute(p, i)
      v.multiplyScalar(1 + 0.2 * Math.sin(v.x * 7) * Math.sin(v.y * 5 + 1) * Math.sin(v.z * 6 + 2))
      p.setXYZ(i, v.x, v.y, v.z)
    }
    ico.computeVertexNormals()
    return [ico, new THREE.ConeGeometry(0.55, 5, 4).translate(0, 2.5, 0)]
  }, [])
  return (
    <>
      <Instanced items={TRUNKS} castShadow receiveShadow><cylinderGeometry args={[0.65, 1, 1, 7]} /><meshStandardMaterial color="#4b3828" roughness={0.95} /></Instanced>
      <Instanced items={CANOPIES} colors={LEAVES} geometry={canopy} castShadow receiveShadow><meshStandardMaterial roughness={0.85} flatShading /></Instanced>
      <Instanced items={FRONDS} geometry={frond} castShadow receiveShadow><meshStandardMaterial color="#56722f" roughness={0.8} flatShading /></Instanced>
    </>
  )
}

function Industry() {
  const t = textures()
  const box = useMemo(() => tiledBox(12, 2.6, 2.4, [1.2, 2.6]), [])
  return (
    <>
      <mesh position={[180, 0.05, -58]} receiveShadow><boxGeometry args={[120, 0.1, 104]} /><meshStandardMaterial color="#8d8b84" roughness={0.95} /></mesh>
      {/* containers + warehouses: painted corrugated steel (bump-mapped ribs, per-instance colour) */}
      <Instanced items={CONTAINERS} colors={TINTS} geometry={box} castShadow receiveShadow>
        <meshStandardMaterial bumpMap={t.ribs} bumpScale={0.8} metalness={0.55} roughness={0.5} />
      </Instanced>
      <group position={[180, 0, 40]}>{/* water tower */}
        <Instanced items={TOWER_LEGS} castShadow><cylinderGeometry args={[1, 1, 1, 8]} /><meshStandardMaterial color="#5a5f64" metalness={0.8} roughness={0.45} /></Instanced>
        <mesh position-y={21.5} castShadow receiveShadow><cylinderGeometry args={[5, 5, 7, 40]} /><meshStandardMaterial color="#cdd5d9" metalness={0.65} roughness={0.3} /></mesh>
        <mesh position-y={26.25} castShadow><coneGeometry args={[5.4, 2.5, 40]} /><meshStandardMaterial color="#9fa8ad" metalness={0.65} roughness={0.35} /></mesh>
      </group>
    </>
  )
}

function Landmarks() {
  return (
    <>
      <group position={[-72, 0.3, 72]}>{/* gas station: aluminium canopy, neon band, a real light underneath */}
        <mesh position-y={6} castShadow receiveShadow><boxGeometry args={[20, 0.9, 12]} /><meshStandardMaterial color="#e6e6e6" metalness={0.6} roughness={0.3} /></mesh>
        <mesh position-y={6.05}><boxGeometry args={[20.2, 0.4, 12.2]} /><meshBasicMaterial color={[6, 0.4, 0.3]} /></mesh>
        <Instanced items={[[-8, 2.8, -5], [8, 2.8, -5], [-8, 2.8, 5], [8, 2.8, 5]].map((p) => [...p, 0.35, 5.6, 0.35])} castShadow>
          <cylinderGeometry args={[1, 1, 1, 12]} /><meshStandardMaterial color="#d0d0d0" metalness={0.9} roughness={0.25} />
        </Instanced>
        <Instanced items={[[-4.5, 0.9, -1.8], [-4.5, 0.9, 1.8], [4.5, 0.9, -1.8], [4.5, 0.9, 1.8]].map((p) => [...p, 0.9, 1.8, 0.6])} castShadow receiveShadow>
          <boxGeometry /><meshStandardMaterial color="#b3121a" metalness={0.3} roughness={0.4} />
        </Instanced>
        <pointLight position-y={5} intensity={40} distance={35} color="#fff2dc" />
        <mesh position={[0, 2.5, -13]} castShadow receiveShadow><boxGeometry args={[16, 5, 7]} /><meshStandardMaterial color="#d8d3c8" roughness={0.9} /></mesh>
        <mesh position={[0, 2, -9.45]}><boxGeometry args={[12, 2.4, 0.1]} /><meshBasicMaterial color={[3.2, 3, 2.6]} /></mesh>
      </group>
      <group position={[24, 0, 72]}>{/* park: lawn, paths and a mirror-chrome sculpture */}
        <mesh position-y={0.15} receiveShadow><boxGeometry args={[36, 0.3, 36]} /><meshStandardMaterial color="#4f6a2c" roughness={1} /></mesh>
        <Instanced items={[[0, 0.31, 0, 36, 0.02, 3], [0, 0.31, 0, 3, 0.02, 36]]} receiveShadow><boxGeometry /><meshStandardMaterial color="#b9b2a5" roughness={0.9} /></Instanced>
        <mesh position-y={2.85} scale={[4.4, 2.5, 2.9]} castShadow><sphereGeometry args={[1, 64, 32]} /><meshStandardMaterial color="#ffffff" metalness={1} roughness={0.03} /></mesh>
      </group>
      <Instanced items={HILLS}><sphereGeometry args={[1, 24, 8, 0, Math.PI * 2, 0, Math.PI / 2]} /><meshStandardMaterial color="#3e4a2c" roughness={1} /></Instanced>
    </>
  )
}

// Sunset dome: gradient, sun disc + glow, drifting fbm cloud deck, horizon melting into the fog colour.
// Hand-rolled to keep its HDR range sane: a Preetham sky at golden hour blooms into white glare at threshold 0.2.
const SKY = {
  uniforms: { sunDir: { value: SUN.clone().normalize() }, haze: { value: new THREE.Color(FOG) }, time: { value: 0 } },
  vertexShader: /* glsl */ `
    varying vec3 vDir;
    void main() {
      vec4 p = modelMatrix * vec4(position, 1.0);
      vDir = p.xyz - cameraPosition;
      gl_Position = projectionMatrix * viewMatrix * p;
    }`,
  fragmentShader: /* glsl */ `
    uniform vec3 sunDir, haze;
    uniform float time;
    varying vec3 vDir;
    float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
    float noise(vec2 p) {
      vec2 i = floor(p), f = fract(p);
      f *= f * (3.0 - 2.0 * f);
      return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
    }
    void main() {
      vec3 d = normalize(vDir);
      float s = max(dot(d, sunDir), 0.0);
      float toSun = dot(normalize(d.xz), normalize(sunDir.xz)) * 0.5 + 0.5;
      vec3 horizon = mix(vec3(0.42, 0.24, 0.32), vec3(1.15, 0.52, 0.2), toSun * toSun);
      vec3 col = mix(horizon, vec3(0.05, 0.11, 0.28), pow(clamp(d.y, 0.0, 1.0), 0.45));
      col += vec3(1.0, 0.5, 0.18) * pow(s, 8.0) * 1.2 + vec3(1.0, 0.85, 0.6) * smoothstep(0.9993, 0.9997, s) * 40.0;
      if (d.y > 0.0) {
        vec2 uv = d.xz / (d.y + 0.1) * 1.5 + vec2(time * 0.004, 0.0);
        float n = 0.0, a = 0.5;
        for (int i = 0; i < 5; i++) { n += a * noise(uv); uv = mat2(1.6, 1.2, -1.2, 1.6) * uv; a *= 0.5; } // rotate octaves: no grid-aligned blocks
        float c = smoothstep(0.45, 0.68, n) * smoothstep(0.02, 0.2, d.y);
        col = mix(col, mix(vec3(0.5, 0.28, 0.36), vec3(1.6, 0.7, 0.35), pow(toSun, 3.0)), c * 0.85);
      }
      gl_FragColor = vec4(mix(haze, col, smoothstep(0.0, 0.1, d.y)), 1.0);
    }`,
}
function SunsetSky() {
  const mat = useRef()
  useFrame((_, dt) => (mat.current.uniforms.time.value += dt))
  return (
    <mesh scale={2500}>
      <sphereGeometry args={[1, 32, 16]} />
      <shaderMaterial ref={mat} args={[SKY]} side={THREE.BackSide} depthWrite={false} />
    </mesh>
  )
}

function Effects() {
  return (
    <EffectComposer multisampling={0}>{/* SMAA instead: 4x MSAA on half-float targets cost ~8 ms on an iGPU */}
      <N8AO halfRes quality="performance" aoRadius={3} distanceFalloff={1} intensity={2.5} />
      <Bloom mipmapBlur intensity={1.5} luminanceThreshold={0.2} luminanceSmoothing={2.5} radius={0.6} />
      <ToneMapping mode={ToneMappingMode.ACES_FILMIC} />
      <Vignette offset={0.3} darkness={0.55} />
      <SMAA />
    </EffectComposer>
  )
}

export default function OpenWorldScene() {
  return (
    <Canvas
      shadows="basic" // PCSS (SoftShadows) needs raw depth, which three r18x only exposes on the basic shadow map
      dpr={[1, 1.5]}
      gl={{ antialias: false, powerPreference: 'high-performance', toneMappingExposure: 0.55 }} // AA is SMAA in the composer
      camera={{ position: [-135, 36, 112], fov: 40, near: 1, far: 3000 }}
      style={{ position: 'fixed', inset: 0, background: '#000' }}
    >
      <fogExp2 attach="fog" args={[FOG, 0.0018]} />
      <SunsetSky />
      <directionalLight
        position={SUN}
        color="#ffb377"
        intensity={12}
        castShadow
        shadow-mapSize={[2048, 2048]}
        shadow-bias={-0.0003}
        shadow-normalBias={0.06}
        shadow-camera-left={-170}
        shadow-camera-right={170}
        shadow-camera-top={140}
        shadow-camera-bottom={-140}
        shadow-camera-near={1}
        shadow-camera-far={800}
      />
      <SoftShadows size={20} samples={14} />
      <Suspense fallback={null}>
        <Environment preset="sunset" background={false} environmentIntensity={0.7} />
        <Ground />
        <Streets />
        <City />
        {CARS.map((car, i) => <Car key={i} car={car} />)}
        <Vegetation />
        <Industry />
        <Landmarks />
        {/* nothing moves, so the shadow map renders once */}
        <BakeShadows />
      </Suspense>
      <Effects />
      <OrbitControls makeDefault target={[0, 24, 0]} enableDamping maxPolarAngle={Math.PI / 2.1} minDistance={20} maxDistance={450} autoRotate autoRotateSpeed={0.3} />
    </Canvas>
  )
}

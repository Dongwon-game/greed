import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

// Input data is the CC0 MakeHuman base.obj, default.mhskel and default_weights.mhw.
// Bake only the visible game geometry, the bind joints and four skin weights per vertex.
const [source, destination] = process.argv.slice(2);
if (!source || !destination) throw new Error('Provide the source data directory and output asset path.');
const rig = JSON.parse(readFileSync(join(source, 'default.mhskel'), 'utf8'));
const weights = JSON.parse(readFileSync(join(source, 'default_weights.mhw'), 'utf8')).weights;
const vertices = [];
const faces = [];
const fittedClothing = [];
let group = '';
for (const line of readFileSync(join(source, 'base.obj'), 'utf8').split(/\r?\n/)) {
    const [token, ...fields] = line.trim().split(/\s+/);
    if (token === 'v') vertices.push(fields.map(Number));
    if (token === 'g') group = fields[0];
    if (token !== 'f') continue;
    const face = fields.map(field => Number(field.split('/')[0]) - 1);
    if (group === 'body') faces.push(face);
    if (group === 'helper-tights') fittedClothing.push(face);
}

const footHeight = Math.min(...faces.flatMap(face => face.map(index => vertices[index][1])));
const rounded = value => Math.round(value * 100000) / 100000;
const meter = ([x, y, z]) => [rounded(x / 10), rounded((y - footHeight) / 10), rounded(z / 10)];
const joints = [];
const jointsByName = new Map();
function addJoint(name) {
    if (jointsByName.has(name)) return jointsByName.get(name);
    const bone = rig.bones[name];
    const parent = bone.parent ? addJoint(bone.parent) : -1;
    const points = rig.joints[bone.head];
    const sum = points.reduce((total, point) => total.map((value, axis) => value + vertices[point][axis]), [0, 0, 0]);
    const [x, y, z] = meter(sum.map(value => value / points.length));
    const index = joints.length;
    jointsByName.set(name, index);
    joints.push({ name, parent, x, y, z });
    return index;
}
for (const name of Object.keys(rig.bones)) addJoint(name);

const weightsByVertex = Array.from({ length: vertices.length }, () => []);
for (const [joint, entries] of Object.entries(weights)) {
    for (const [vertex, weight] of entries) weightsByVertex[vertex].push([jointsByName.get(joint), weight]);
}
const positions = [];
const colors = [];
const triangles = [];
const influences = [];
const strengths = [];
const bodyIndices = new Map();
const clothIndices = new Map();

function writeVertex(sourceIndex, clothing) {
    const ids = clothing ? clothIndices : bodyIndices;
    if (ids.has(sourceIndex)) return ids.get(sourceIndex);
    const [x, y, z] = meter(vertices[sourceIndex]);
    const index = positions.length / 3;
    ids.set(sourceIndex, index);
    positions.push(x, y, z);
    if (clothing) {
        const stripe = Math.abs(x) < 0.033 && z < 0.05;
        const belt = y > 0.91 && y < 1.01;
        const collar = y > 1.355 && Math.abs(x) < 0.18;
        const dark = y < 0.93;
        const color = collar ? [187, 129, 60]
            : stripe || belt || dark ? [36, 104, 101] : [202, 182, 144];
        const pleat = 0.94 + 0.06 * Math.cos(Math.atan2(x, z - 0.035) * 14);
        colors.push(...color.map(value => Math.round(value * pleat)));
    } else {
        const hair = y > 1.49 && z < 0.16 && (y > 1.56 || z < 0.015);
        const sandals = y < 0.049;
        const cuff = Math.abs(x) > 0.37 && y > 1.045 && y < 1.084;
        colors.push(...(hair ? [42, 29, 24] : sandals ? [93, 59, 37] : cuff ? [187, 129, 60] : [173, 115, 81]));
    }

    const best = weightsByVertex[sourceIndex].sort((a, b) => b[1] - a[1]).slice(0, 4);
    if (best.length === 0) throw new Error(`Missing skin weights at source vertex ${sourceIndex}.`);
    const sum = best.reduce((total, entry) => total + entry[1], 0);
    for (let i = 0; i < 4; i++) {
        influences.push(best[i]?.[0] ?? 0);
        strengths.push(best[i] ? rounded(best[i][1] / sum) : 0);
    }
    return index;
}

function triangulate(face, clothing) {
    const ids = face.map(index => writeVertex(index, clothing));
    for (let i = 1; i + 1 < ids.length; i++) triangles.push(ids[0], ids[i], ids[i + 1]);
}

function isUnderTunica([x, y]) {
    return y > -2 && y < 5.65 && Math.abs(x) < 1.98;
}
for (const face of faces) {
    // Omit the concealed body surface and any internal geometry around the hips.
    if (!face.every(index => isUnderTunica(vertices[index]))) triangulate(face, false);
}
for (const face of fittedClothing) {
    if (face.some(index => isUnderTunica(vertices[index]))) triangulate(face, true);
}

// The braided tail sits off the back of the head and follows the authored head joint.
const headJoint = jointsByName.get('head');
const braidStart = positions.length / 3;
const braidSegments = 20;
const braidSides = 8;
for (let ring = 0; ring <= braidSegments; ring++) {
    const t = ring / braidSegments;
    const radius = (0.025 - 0.012 * t) * (1 + 0.11 * Math.cos(t * 19 * Math.PI));
    for (let side = 0; side < braidSides; side++) {
        const angle = side / braidSides * 2 * Math.PI;
        positions.push(rounded(0.035 + 0.105 * t * t + radius * Math.cos(angle)),
            rounded(1.59 - 0.245 * t), rounded(-0.071 - 0.11 * t + radius * Math.sin(angle)));
        const glint = Math.max(0, Math.sin(angle + t * 42)) * 14;
        colors.push(Math.round(34 + glint), Math.round(24 + glint * 0.68), Math.round(19 + glint * 0.45));
        influences.push(headJoint, 0, 0, 0);
        strengths.push(1, 0, 0, 0);
        if (ring === braidSegments) continue;
        const a = braidStart + ring * braidSides + side;
        const b = braidStart + ring * braidSides + (side + 1) % braidSides;
        triangles.push(a, b, b + braidSides, a, b + braidSides, a + braidSides);
    }
}
writeFileSync(destination, JSON.stringify({ joints, positions, colors, triangles, influences, strengths }));
console.log(`${positions.length / 3} vertices, ${triangles.length / 3} triangles, ${joints.length} joints`);

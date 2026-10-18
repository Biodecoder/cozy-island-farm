#!/usr/bin/env node
/**
 * Generates the 10 approved Cozy Island Farm 3D assets via Tripo CLI/MCP,
 * preserves both native GLB + preview.png and converted FBX, and writes
 * TripoSource/tripo_manifest.json with the complete itemized credit ledger.
 */
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const ROOT_DIR = '/Users/derya/Gemini-4/cozy-island-farm';
const OUT_DIR = path.join(ROOT_DIR, 'TripoSource');
const MANIFEST_PATH = path.join(OUT_DIR, 'tripo_manifest.json');
const TRIPO_BIN = '/Users/derya/.npm/_npx/a1896cbe393e8b7c/node_modules/.bin/tripo';

fs.mkdirSync(OUT_DIR, { recursive: true });

const ASSETS = [
  {
    name: 'farmer',
    prompt: 'chibi cozy farmer character in blue denim overalls, white shirt, brown boots, and wide straw sunhat, stylized 3D game character, T-pose, standing straight',
    args: ['--for', 'anim'],
    category: 'anim'
  },
  {
    name: 'barn',
    prompt: 'cozy stylized red wooden farm barn with dark grey gambrel roof, white trim, white X-braced double doors, and small roof cupola, cute 3D farm game building',
    args: ['-p', 'face_limit=12000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'windmill',
    prompt: 'cozy stylized medieval farm windmill tower with whitewashed plaster walls, cobblestone base, dark shingled dome roof, and four wooden lattice sails, cute 3D game building',
    args: ['-p', 'face_limit=12000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'market_stall',
    prompt: 'cozy wooden farmer market stall booth with red and white striped fabric awning roof and wooden display crates of fruit, stylized 3D game prop',
    args: ['-p', 'face_limit=10000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'wooden_bridge',
    prompt: 'cozy curved arched wooden footbridge with timber railings and plank deck, stylized 3D farm game prop',
    args: ['-p', 'face_limit=8000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'crop_pumpkin',
    prompt: 'plump stylized orange pumpkin with ribbed skin and curly green vine leaf on top, cute 3D farm game crop',
    args: ['-p', 'face_limit=4000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'crop_tomato',
    prompt: 'stylized lush green tomato bush plant bearing ripe red-orange tomatoes, cute 3D farm game crop',
    args: ['-p', 'face_limit=5000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'apple_tree',
    prompt: 'cozy stylized puffy green broadleaf apple tree with bright red apples hanging in the foliage and warm brown trunk, cute 3D game tree',
    args: ['-p', 'face_limit=8000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'street_lantern',
    prompt: 'cozy iron shepherd crook street lamp post with warm glowing glass lantern and wooden base crate, stylized 3D farm game prop',
    args: ['-p', 'face_limit=5000', '--then', 'convert:fbx'],
    category: 'prop'
  },
  {
    name: 'watering_can',
    prompt: 'cozy blue metal garden watering can with curved handle and rose spout, stylized 3D farm game tool',
    args: ['-p', 'face_limit=3000', '--then', 'convert:fbx'],
    category: 'prop'
  }
];

function runTripo(cliArgs) {
  return new Promise((resolve, reject) => {
    const proc = spawn(TRIPO_BIN, cliArgs, {
      cwd: ROOT_DIR,
      env: {
        ...process.env,
        PATH: '/opt/homebrew/bin:/opt/homebrew/sbin:/Users/derya/.local/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin'
      }
    });
    let stdout = '';
    let stderr = '';
    proc.stdout.on('data', (d) => { stdout += d.toString(); });
    proc.stderr.on('data', (d) => {
      const text = d.toString();
      stderr += text;
      process.stderr.write(text);
    });
    proc.on('close', (code) => {
      const lines = stdout.trim().split('\n').filter(Boolean);
      const lastLine = lines[lines.length - 1] || '{}';
      let parsed = null;
      try {
        parsed = JSON.parse(lastLine);
      } catch {
        parsed = { raw: stdout };
      }
      resolve({ code, parsed, stdout, stderr });
    });
    proc.on('error', reject);
  });
}

async function generateSingleAsset(asset) {
  const targetDir = path.join(OUT_DIR, asset.name);
  fs.mkdirSync(targetDir, { recursive: true });

  // Resume support: if already generated with both model.glb and model.fbx, skip
  const existingReceipt = path.join(targetDir, 'receipt.json');
  if (fs.existsSync(existingReceipt)) {
    try {
      const saved = JSON.parse(fs.readFileSync(existingReceipt, 'utf8'));
      if (saved.status === 'success' && fs.existsSync(saved.glb_path)) {
        console.log(`[SKIP] ${asset.name} already completed.`);
        return saved;
      }
    } catch {}
  }

  console.log(`[START] Generating ${asset.name}...`);
  const cmdArgs = [
    'make',
    asset.prompt,
    ...asset.args,
    '--name', asset.name,
    '-o', targetDir,
    '--json',
    '--yes'
  ];

  let res = await runTripo(cmdArgs);
  if (res.code !== 0 && asset.name === 'farmer') {
    console.log(`[FALLBACK] Farmer anim chain exited ${res.code}; generating v3.1 mesh + convert:fbx...`);
    res = await runTripo([
      'make',
      asset.prompt,
      '--model', 'tripo-v3.1',
      '--then', 'convert:fbx',
      '--name', asset.name,
      '-o', targetDir,
      '--json',
      '--yes'
    ]);
  }

  if (res.code !== 0 || !res.parsed || res.parsed.status !== 'success') {
    throw new Error(`Asset ${asset.name} failed (exit ${res.code}): ${JSON.stringify(res.parsed)}`);
  }

  const result = res.parsed;
  const finalModelPath = path.resolve(targetDir, result.model_file || '');
  const ext = path.extname(finalModelPath).toLowerCase();
  const fbxDest = path.join(targetDir, `${asset.name}.fbx`);
  const glbDest = path.join(targetDir, `${asset.name}.glb`);
  const previewDest = path.join(targetDir, `${asset.name}_preview.png`);

  if (fs.existsSync(finalModelPath)) {
    if (ext === '.fbx') {
      fs.copyFileSync(finalModelPath, fbxDest);
    } else if (ext === '.glb') {
      fs.copyFileSync(finalModelPath, glbDest);
    }
  }

  if (result.preview && fs.existsSync(path.resolve(targetDir, result.preview))) {
    fs.copyFileSync(path.resolve(targetDir, result.preview), previewDest);
  }

  // Also fetch the upstream generation task's native GLB and preview.png (free download!)
  const sourceTaskId = result.source_task_id || (result.credits_breakdown && result.credits_breakdown[0]?.task_id);
  if (sourceTaskId && (!fs.existsSync(glbDest) || !fs.existsSync(previewDest))) {
    const sourceDlDir = path.join(targetDir, 'source_glb');
    fs.mkdirSync(sourceDlDir, { recursive: true });
    const dlRes = await runTripo(['task', 'get', sourceTaskId, '--download', '-o', sourceDlDir, '--json']);
    if (dlRes.code === 0) {
      const files = fs.readdirSync(sourceDlDir);
      const glbFile = files.find((f) => f.endsWith('.glb'));
      const pngFile = files.find((f) => f.endsWith('.png') || f.endsWith('.webp') || f.endsWith('.jpg'));
      if (glbFile && !fs.existsSync(glbDest)) {
        fs.copyFileSync(path.join(sourceDlDir, glbFile), glbDest);
      }
      if (pngFile && !fs.existsSync(previewDest)) {
        fs.copyFileSync(path.join(sourceDlDir, pngFile), previewDest);
      }
    }
  }

  const receipt = {
    name: asset.name,
    prompt: asset.prompt,
    status: result.status,
    task_id: result.task_id,
    source_task_id: sourceTaskId || result.task_id,
    credits_consumed: result.credits_consumed ?? 0,
    credits_breakdown: result.credits_breakdown || [],
    fbx_path: fs.existsSync(fbxDest) ? fbxDest : null,
    glb_path: fs.existsSync(glbDest) ? glbDest : null,
    preview_path: fs.existsSync(previewDest) ? previewDest : null,
    raw_result: result
  };

  fs.writeFileSync(existingReceipt, JSON.stringify(receipt, null, 2));
  console.log(`[DONE] ${asset.name} (${receipt.credits_consumed} credits)`);
  return receipt;
}

async function main() {
  const startBal = await runTripo(['balance', '--json']);
  console.log('[BALANCE BEFORE]', startBal.parsed);

  const farmerAsset = ASSETS.find((a) => a.name === 'farmer');
  const propAssets = ASSETS.filter((a) => a.name !== 'farmer');

  const results = [];

  // Run farmer (v3.1/anim pool) in parallel with a 3-worker queue for P1 props
  const farmerPromise = generateSingleAsset(farmerAsset).then((r) => results.push(r));

  const queue = [...propAssets];
  const workers = Array.from({ length: 3 }, async () => {
    while (queue.length > 0) {
      const next = queue.shift();
      if (!next) break;
      const r = await generateSingleAsset(next);
      results.push(r);
    }
  });

  await Promise.all([farmerPromise, ...workers]);

  const endBal = await runTripo(['balance', '--json']);
  const totalCredits = results.reduce((sum, r) => sum + (Number(r.credits_consumed) || 0), 0);

  const manifest = {
    generated_at: new Date().toISOString(),
    balance_before: startBal.parsed,
    balance_after: endBal.parsed,
    total_credits_consumed: totalCredits,
    assets: results
  };

  fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 2));
  console.log(`[ALL 10 ASSETS COMPLETE] Total billed credits: ${totalCredits}`);
}

main().catch((err) => {
  console.error('[FATAL]', err);
  process.exit(1);
});

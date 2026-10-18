#!/usr/bin/env node
/**
 * Batch-fetches all 10 generated Tripo tasks in a single `tripo task list` call,
 * downloads every `.glb`, `.fbx`, and preview image using `curl -4 -sSL` in parallel,
 * and writes TripoSource/tripo_manifest.json.
 */
import { execFileSync, spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const ROOT_DIR = '/Users/derya/Gemini-4/cozy-island-farm';
const OUT_DIR = path.join(ROOT_DIR, 'TripoSource');
const TRIPO_BIN = '/Users/derya/.npm/_npx/a1896cbe393e8b7c/node_modules/.bin/tripo';

const TASK_MAP = [
  {
    name: 'farmer',
    glb_task: 'def169b6-0070-4b20-b0bc-8deaef4f72dd',
    rig_task: 'bc23b98d-1873-4c8a-bab4-9564a275e866',
    alt_glb_task: '32bb058a-bdea-400f-b2ab-805ed1faf18d'
  },
  {
    name: 'barn',
    glb_task: '94b7a549-35c2-4c23-9e85-8b47aebc7942',
    fbx_task: 'c7b63c99-66e8-4d9f-ab82-ab431c7031ac'
  },
  {
    name: 'windmill',
    glb_task: 'a7d75674-c123-413b-9dfd-112125648fe5',
    fbx_task: 'fcb84e43-43cb-4c56-8f34-229ce9236439'
  },
  {
    name: 'market_stall',
    glb_task: '5df37db7-56a0-4426-ad75-5e7a74f33928',
    fbx_task: 'bc7acdb2-baf3-402f-a723-d7818a52fa8a'
  },
  {
    name: 'wooden_bridge',
    glb_task: '8dd48858-a637-453a-a232-4052cdaab481',
    fbx_task: 'db6744e2-2276-4f50-a0f6-517ce75f91d4'
  },
  {
    name: 'crop_pumpkin',
    glb_task: '726d3c39-baf6-47d5-b7f1-1868f9b0d9cb',
    fbx_task: '17a2acdc-13ce-422c-a1df-62a75dc6ad1b'
  },
  {
    name: 'crop_tomato',
    glb_task: 'e9e0c2de-c22b-4593-afac-89298d98c6bf',
    fbx_task: 'bceba88a-50fc-4be2-8775-8af92c231293'
  },
  {
    name: 'apple_tree',
    glb_task: '259becb0-cfd9-4b2a-8f55-5a9c568e499e'
  },
  {
    name: 'street_lantern',
    glb_task: '3084acec-41b4-4a54-8d25-3b297dee7152',
    fbx_task: '959078df-6630-4f26-a416-dd54a74a9da9'
  },
  {
    name: 'watering_can',
    glb_task: '028af63a-bf08-4e14-9d0d-d92440bd2bf5'
  }
];

function curlDownload(url, destPath) {
  return new Promise((resolve) => {
    if (!url) return resolve(false);
    if (fs.existsSync(destPath) && fs.statSync(destPath).size > 1000) {
      return resolve(true);
    }
    const p = spawn('/usr/bin/curl', ['-4', '-sSL', '--retry', '3', '--max-time', '45', url, '-o', destPath]);
    p.on('close', (code) => resolve(code === 0 && fs.existsSync(destPath) && fs.statSync(destPath).size > 500));
  });
}

async function main() {
  const allIds = [];
  for (const item of TASK_MAP) {
    if (item.glb_task) allIds.push(item.glb_task);
    if (item.rig_task) allIds.push(item.rig_task);
    if (item.alt_glb_task) allIds.push(item.alt_glb_task);
    if (item.fbx_task) allIds.push(item.fbx_task);
  }

  console.log(`[BATCH QUERY] Fetching metadata for ${allIds.length} tasks...`);
  const listOut = execFileSync(TRIPO_BIN, ['task', 'list', ...allIds, '--json'], {
    cwd: ROOT_DIR,
    env: {
      ...process.env,
      NODE_OPTIONS: '--dns-result-order=ipv4first',
      PATH: '/opt/homebrew/bin:/opt/homebrew/sbin:/Users/derya/.local/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin'
    },
    encoding: 'utf8'
  });

  const parsedList = JSON.parse(listOut.trim().split('\n').pop());
  const tasksArray = Array.isArray(parsedList) ? parsedList : (parsedList.tasks || []);
  const byId = new Map(tasksArray.map((t) => [t.task_id, t]));

  const manifestEntries = [];
  let grandTotalCredits = 0;

  await Promise.all(TASK_MAP.map(async (item) => {
    const assetDir = path.join(OUT_DIR, item.name);
    fs.mkdirSync(assetDir, { recursive: true });

    const glbMeta = byId.get(item.glb_task);
    const rigMeta = item.rig_task ? byId.get(item.rig_task) : null;
    const altMeta = item.alt_glb_task ? byId.get(item.alt_glb_task) : null;
    const fbxMeta = item.fbx_task ? byId.get(item.fbx_task) : null;

    const glbTarget = path.join(assetDir, `${item.name}.glb`);
    const fbxTarget = path.join(assetDir, `${item.name}.fbx`);
    const previewTarget = path.join(assetDir, `${item.name}_preview.webp`);

    const glbUrl = glbMeta?.output?.model_url || glbMeta?.output?.pbr_model || rigMeta?.output?.model_url || altMeta?.output?.model_url;
    const fbxUrl = fbxMeta?.output?.model_url || fbxMeta?.output?.model;
    const previewUrl = glbMeta?.output?.rendered_image_url || glbMeta?.output?.generated_image_url || altMeta?.output?.rendered_image_url;

    await Promise.all([
      curlDownload(glbUrl, glbTarget),
      curlDownload(fbxUrl, fbxTarget),
      curlDownload(previewUrl, previewTarget)
    ]);

    const breakdown = [];
    for (const m of [glbMeta, rigMeta, altMeta, fbxMeta]) {
      if (m && m.status === 'success') {
        breakdown.push({
          task_id: m.task_id,
          type: m.type,
          credits_consumed: Number(m.credits_consumed ?? 0)
        });
      }
    }

    const assetCredits = breakdown.reduce((s, b) => s + b.credits_consumed, 0);
    grandTotalCredits += assetCredits;

    const entry = {
      name: item.name,
      credits_consumed: assetCredits,
      credits_breakdown: breakdown,
      glb_path: fs.existsSync(glbTarget) ? glbTarget : null,
      fbx_path: fs.existsSync(fbxTarget) ? fbxTarget : null,
      preview_path: fs.existsSync(previewTarget) ? previewTarget : null
    };
    manifestEntries.push(entry);
    console.log(`[DOWNLOADED] ${item.name}: glb=${Boolean(entry.glb_path)} (${entry.glb_path ? fs.statSync(entry.glb_path).size : 0}B), fbx=${Boolean(entry.fbx_path)}, credits=${assetCredits}`);
  }));

  const balOut = execFileSync(TRIPO_BIN, ['balance', '--json'], {
    cwd: ROOT_DIR,
    env: {
      ...process.env,
      NODE_OPTIONS: '--dns-result-order=ipv4first',
      PATH: '/opt/homebrew/bin:/opt/homebrew/sbin:/Users/derya/.local/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin'
    },
    encoding: 'utf8'
  });
  const finalBalance = JSON.parse(balOut.trim().split('\n').pop());

  const manifest = {
    collected_at: new Date().toISOString(),
    balance_before: { balance: 2000, frozen: 0 },
    balance_after: finalBalance,
    total_credits_consumed: grandTotalCredits,
    assets: manifestEntries
  };

  fs.writeFileSync(path.join(OUT_DIR, 'tripo_manifest.json'), JSON.stringify(manifest, null, 2));
  console.log(`[COMPLETE] Total credits billed=${grandTotalCredits}, Remaining balance=${JSON.stringify(finalBalance)}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

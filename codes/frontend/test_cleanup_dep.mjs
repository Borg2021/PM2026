const TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEiLCJyZWFsTmFtZSI6Iuezu-e7n-euoeeQhuWRmCIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6ImFkbWluIiwiZXhwIjoxNzc5NDU1MDExfQ.V8M6kuTVlvXPP0_gD1xu2DLnacEHaN12SWGSKadzp-c';

// Get current tasks to see what changed
const r = await fetch('http://localhost:5292/api/v1/projects/1/tasks', {
  headers: { 'Authorization': `Bearer ${TOKEN}` }
});
const data = await r.json();
for (const t of data.data) {
  console.log(`id=${t.id} no=${t.taskNo} name=${t.taskName} preTaskCodes=${t.preTaskCodes} planStart=${t.planStartDate?.slice(0,10)} planFinish=${t.planFinishDate?.slice(0,10)}`);
}

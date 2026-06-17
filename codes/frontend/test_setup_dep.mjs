// Set up a dependency for testing
const TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEiLCJyZWFsTmFtZSI6Iuezu-e7n-euoeeQhuWRmCIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6ImFkbWluIiwiZXhwIjoxNzc5NDU1MDExfQ.V8M6kuTVlvXPP0_gD1xu2DLnacEHaN12SWGSKadzp-c';

// First update task 117 (003.2) with dates
const r1 = await fetch('http://localhost:5292/api/v1/projects/1/tasks/117', {
  method: 'PUT',
  headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
  body: JSON.stringify({
    taskNo: '003.2', wbsCode: '', taskName: 'test1', nodeType: 1, sortOrder: 1,
    status: 1, priority: 3, planStartDate: '2026-05-15T00:00:00', planFinishDate: '2026-05-22T00:00:00',
    planDuration: 7, progressPct: 0, deliverableCnt: 0, parentId: 116
  })
});
console.log('Update 117:', r1.status, await r1.text());

// Now set predecessor on task 122
const r2 = await fetch('http://localhost:5292/api/v1/projects/1/tasks/122', {
  method: 'PUT',
  headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
  body: JSON.stringify({
    taskNo: '003', wbsCode: '', taskName: '设计电气料单', nodeType: 1, sortOrder: 3,
    status: 1, priority: 3, planStartDate: '2026-05-20T00:00:00', planFinishDate: '2026-05-28T00:00:00',
    planDuration: 8, progressPct: 80, preTaskCodes: '003.2(FS)', deliverableCnt: 0
  })
});
console.log('Update 122:', r2.status, await r2.text());

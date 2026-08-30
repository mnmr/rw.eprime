---
title: Filters and search
---
The filter row narrows the table without touching any assignments:

![The filter row](filter-row.png)

- **Search** matches colonist names. Every space-separated term must
match, and prefixes widen the hunt: r: or role: searches assigned role
names, j: or job: searches the jobs those roles supply. "br r:doc" finds
colonists named *br-something* holding a doctor-ish role.
- **Assigned role** lists colonists holding the chosen role, or any
non-[blocker](topic:blocker-roles) role that fully covers its jobs.
- **Covered job** lists colonists whose non-blocker roles include the
chosen job.
- **Location** picks which colony supplies the table: the current map,
everywhere, a settlement, or the ship. Colonists out with a caravan
count under the place they left from.

Filters combine, and the clear button that appears once anything is
active resets the search and the role and job filters while leaving
location, grouping, and display choices alone.

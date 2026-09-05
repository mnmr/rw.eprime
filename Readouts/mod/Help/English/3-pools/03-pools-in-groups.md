---
title: Pools in groups
---
In the Group Editor, pools sit under the Resource Pools branch at the top of the Resources panel. Add one to a group the same way as a resource: click it, or drag it onto a slot (see [Tiers in the Group Editor](topic:tiers-in-the-group-editor)). A search for "meal" finds the Meals pool under that branch, already in the group:

![The Resource Pools branch with the Meals pool in the group](resources-search.png)

A pool slot behaves like any other slot. It can have thresholds, hide itself at zero, and override the count options (see [Slot options](topic:slot-options)). The pool's own count override applies to all of its members; overrides set on a member for its own slot do not reach into the pool.

Deleting a pool removes its slot from every group that used it, together with its thresholds and count overrides.

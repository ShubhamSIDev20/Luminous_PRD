@* DashboardComponent.razor *@
@using Blazicons.Lucide

<div class="space-y-6">
    <div class="flex items-center justify-between">
        <h2 class="text-2xl font-bold">Dashboard</h2>
        <div class="flex gap-2">
            <button class="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition-colors">
                <Blazicon  Svg="Lucide.RefreshCw" class="w-4 h-4 inline mr-2" />
                Refresh
            </button>
        </div>
    </div>
    
    <!-- Stats Cards -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-card border border-border rounded-lg p-6 shadow-sm hover:shadow-md transition-shadow">
            <div class="flex items-center justify-between mb-2">
                <span class="text-sm text-muted-foreground">Total Users</span>
                <Blazicon  Svg="Lucide.Users" class="w-5 h-5 text-primary" />
            </div>
            <div class="text-3xl font-bold">1,234</div>
            <div class="text-xs text-success mt-1">+12% from last month</div>
        </div>
        
        <div class="bg-card border border-border rounded-lg p-6 shadow-sm hover:shadow-md transition-shadow">
            <div class="flex items-center justify-between mb-2">
                <span class="text-sm text-muted-foreground">Revenue</span>
                <Blazicon  Svg="Lucide.DollarSign" class="w-5 h-5 text-primary" />
            </div>
            <div class="text-3xl font-bold">$45,231</div>
            <div class="text-xs text-success mt-1">+8% from last month</div>
        </div>
        
        <div class="bg-card border border-border rounded-lg p-6 shadow-sm hover:shadow-md transition-shadow">
            <div class="flex items-center justify-between mb-2">
                <span class="text-sm text-muted-foreground">Active Sessions</span>
                <Blazicon  Svg="Lucide.Activity" class="w-5 h-5 text-primary" />
            </div>
            <div class="text-3xl font-bold">573</div>
            <div class="text-xs text-warning mt-1">-3% from last hour</div>
        </div>
        
        <div class="bg-card border border-border rounded-lg p-6 shadow-sm hover:shadow-md transition-shadow">
            <div class="flex items-center justify-between mb-2">
                <span class="text-sm text-muted-foreground">Success Rate</span>
                <Blazicon  Svg="Lucide.TrendingUp" class="w-5 h-5 text-primary" />
            </div>
            <div class="text-3xl font-bold">98.5%</div>
            <div class="text-xs text-success mt-1">+2% from last week</div>
        </div>
    </div>
    
    <!-- Recent Activity -->
    <div class="bg-card border border-border rounded-lg shadow-sm">
        <div class="px-6 py-4 border-b border-border">
            <h3 class="text-lg font-semibold">Recent Activity</h3>
        </div>
        <div class="p-6">
            <div class="space-y-4">
                @for (int i = 1; i <= 5; i++)
                {
                    <div class="flex items-center gap-4 p-3 rounded-lg hover:bg-accent transition-colors">
                        <div class="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
                            <Blazicon  Svg="Lucide.User" class="w-5 h-5 text-primary" />
                        </div>
                        <div class="flex-1">
                            <p class="font-medium">User Action @i</p>
                            <p class="text-sm text-muted-foreground">Description of the activity</p>
                        </div>
                        <span class="text-xs text-muted-foreground">@i min ago</span>
                    </div>
                }
            </div>
        </div>
    </div>
</div>

@* ================================================================ *@

@* SettingsComponent.razor *@
@using Blazicons.Lucide

<div class="space-y-6">
    <h2 class="text-2xl font-bold">Settings</h2>
    
    <div class="bg-card border border-border rounded-lg shadow-sm">
        <div class="px-6 py-4 border-b border-border">
            <h3 class="text-lg font-semibold">General Settings</h3>
        </div>
        <div class="p-6 space-y-6">
            <!-- Application Name -->
            <div>
                <label class="block text-sm font-medium mb-2">Application Name</label>
                <input type="text" 
                       value="My Application" 
                       class="w-full px-3 py-2 bg-input border border-border rounded-md focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            
            <!-- Language -->
            <div>
                <label class="block text-sm font-medium mb-2">Language</label>
                <select class="w-full px-3 py-2 bg-input border border-border rounded-md focus:outline-none focus:ring-2 focus:ring-ring">
                    <option>English</option>
                    <option>Spanish</option>
                    <option>French</option>
                    <option>German</option>
                </select>
            </div>
            
            <!-- Notifications -->
            <div class="flex items-center justify-between">
                <div>
                    <label class="block text-sm font-medium">Email Notifications</label>
                    <p class="text-sm text-muted-foreground">Receive notifications via email</p>
                </div>
                <label class="relative inline-flex items-center cursor-pointer">
                    <input type="checkbox" class="sr-only peer" checked />
                    <div class="w-11 h-6 bg-muted peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-ring rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-primary"></div>
                </label>
            </div>
            
            <!-- Auto-save -->
            <div class="flex items-center justify-between">
                <div>
                    <label class="block text-sm font-medium">Auto-save</label>
                    <p class="text-sm text-muted-foreground">Automatically save changes</p>
                </div>
                <label class="relative inline-flex items-center cursor-pointer">
                    <input type="checkbox" class="sr-only peer" checked />
                    <div class="w-11 h-6 bg-muted peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-ring rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-primary"></div>
                </label>
            </div>
            
            <!-- Save Button -->
            <div class="flex justify-end gap-2 pt-4">
                <button class="px-4 py-2 bg-secondary text-secondary-foreground rounded-md hover:bg-secondary/90 transition-colors">
                    Cancel
                </button>
                <button class="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition-colors">
                    <Blazicon  Svg="Lucide.Save" class="w-4 h-4 inline mr-2" />
                    Save Changes
                </button>
            </div>
        </div>
    </div>
</div>

@* ================================================================ *@

@* DataGridComponent.razor (Example with your grid styles) *@
@using Blazicons.Lucide

<div class="space-y-6">
    <div class="flex items-center justify-between">
        <h2 class="text-2xl font-bold">Data Grid</h2>
        <button class="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition-colors">
            <Blazicon  Svg="Lucide.Plus" class="w-4 h-4 inline mr-2" />
            Add New
        </button>
    </div>
    
    <div class="bg-card border border-grid-border rounded-lg shadow-sm overflow-hidden">
        <!-- Grid Header -->
        <div class="grid grid-cols-5 bg-grid-header border-b border-grid-border">
            <div class="grid-header-cell">ID</div>
            <div class="grid-header-cell">Name</div>
            <div class="grid-header-cell">Status</div>
            <div class="grid-header-cell">Date</div>
            <div class="grid-header-cell">Actions</div>
        </div>
        
        <!-- Grid Rows -->
        @for (int i = 1; i <= 10; i++)
        {
            var rowClass = i % 2 == 0 ? "bg-grid-row" : "bg-grid-row-alt";
            <div class="grid grid-cols-5 @rowClass hover:bg-grid-row-hover border-b border-grid-border last:border-b-0 transition-colors">
                <div class="grid-cell">@i</div>
                <div class="grid-cell">Item @i</div>
                <div class="grid-cell">
                    <span class="operator-badge @(i % 3 == 0 ? "bg-success text-white" : i % 3 == 1 ? "bg-warning text-white" : "bg-muted text-muted-foreground")">
                        @(i % 3 == 0 ? "Active" : i % 3 == 1 ? "Pending" : "Inactive")
                    </span>
                </div>
                <div class="grid-cell">2024-01-@(i.ToString("00"))</div>
                <div class="grid-cell flex gap-2">
                    <button class="action-button" title="Edit">
                        <Blazicon  Svg="Lucide.Edit" class="w-4 h-4" />
                    </button>
                    <button class="action-button" title="Delete">
                        <Blazicon  Svg="Lucide.Trash2" class="w-4 h-4" />
                    </button>
                    <button class="action-button" title="More">
                        <Blazicon  Svg="Lucide.MoreHorizontal" class="w-4 h-4" />
                    </button>
                </div>
            </div>
        }
    </div>
</div>
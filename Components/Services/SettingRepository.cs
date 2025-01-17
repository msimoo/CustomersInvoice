﻿using CustomersManagementApp.Components.DataContext;
using CustomersManagementApp.Components.Entities;
using CustomersManagementApp.Components.Services.Interfaces;

using Microsoft.EntityFrameworkCore;

using System.Threading.Tasks;

namespace CustomersManagementApp.Components.Services {
	public class SettingRepository : ISettingRepository
    {
		private readonly InvoiceContext _context;

		public SettingRepository(InvoiceContext context) {
			this._context = context;
		}

		public async Task<Settings> GetSettings()
        {
            var response = await _context.Settings.FirstOrDefaultAsync();
            return response;
        }

        public async Task<Settings> Update(Settings settings)
        {
            var settingsBeforeUpdate = await _context.Settings.FirstOrDefaultAsync();
            if (settingsBeforeUpdate == null)
            {
                return null;
            }

            _context.Entry(settingsBeforeUpdate).CurrentValues.SetValues(settings);
            await _context.SaveChangesAsync();

            return settings;
        }
    }
}

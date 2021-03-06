﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FluentAutomation.Interfaces;

namespace FluentAutomation
{
    public class WbTstrSelectSyntaxProvider : ISelectSyntaxProvider
    {
        private readonly IActionSyntaxProvider _actionSyntaxProvider;
        private readonly ISelectSyntaxProvider _selectSyntaxProvider;
        private readonly ILogger _logger;

        internal WbTstrSelectSyntaxProvider(WbTstrActionSyntaxProvider actionSyntaxProvider, ISelectSyntaxProvider selectSyntaxProvider, ILogger logger)
        {
            _actionSyntaxProvider = actionSyntaxProvider;
            _selectSyntaxProvider = selectSyntaxProvider;
            _logger = logger;
        }

        /*-------------------------------------------------------------------*/

        public bool IsInDryRunMode
        {
            get
            {
                return FluentSettings.Current.IsDryRun;
            }
        }

        /*-------------------------------------------------------------------*/

        public IActionSyntaxProvider From(string selector)
        {
            return From(_actionSyntaxProvider.Find(selector));
        }

        public IActionSyntaxProvider From(ElementProxy element)
        {
            // Before
            string selector = (element != null && element.Element != null) ? element.Element.Selector ?? "?" : "?";
            _logger.LogMessage("Perform selection in element with selector: {0}", selector); 

            // Execute
            if (!IsInDryRunMode)
            {
                _selectSyntaxProvider.From(element);
            }

            // After
            return _actionSyntaxProvider;
        }
    }
}
# Retail Store POS Application Report

## 1. Project Overview

**Project Name:** Retail Store POS  
**POS Meaning:** Point of Sale  
**Platform:** Windows desktop application  
**Supported Operating Systems:** Windows 10 and Windows 11

Retail Store POS is a Windows-based point-of-sale application designed for retail store operations. The application is currently working properly on Windows 10 and Windows 11. Its main purpose is to help store staff process sales, manage cashier sessions, open and close registers, track payments, print or export receipts, and support fast checkout workflows.

The system is designed around practical retail usage. It supports fast cashier access, manager-level login, register opening and closing, cash tracking, sales processing, discounts, receipt handling, inventory alerts, and session summaries.

This document currently covers the application flow from startup and login through the checkout page, register opening, payments, receipts, inventory alerts, and register closing. Additional pages, including Products, Reports, Settings, Store Management, Tax Configuration, Users and Roles, My Preference, About, Sign Out, and the top-right user control area, will be documented in the next section after further details are provided.

---

## 2. Application Startup and Splash Screen

When the application starts, the user first sees a **splash screen**. This screen appears during application loading and gives the user an initial startup experience before entering the main login screen.

After the splash screen, the user is presented with the login interface. The login interface is divided into two main sections:

1. **Cashier fast login section**
2. **Manager login section**

The purpose of this separation is to support two different login experiences: a fast workflow for cashiers and a more traditional credential-based workflow for managers.

---

## 3. Login System

### 3.1 Cashier Fast Login Section

The cashier login section is designed to allow cashiers to log in quickly without needing to type many details. Instead of entering a full username, the cashier selects their profile card from the screen and then enters a PIN code.

Each cashier appears as a card. The card may contain the following information:

- Badge photo placeholder
- First name of the cashier
- User role, such as Cashier, Manager, or Administrator
- Cashier name

At the moment, the badge photo area exists, but there is no photo currently displayed.

The cashier login process works as follows:

1. The cashier selects their user card.
2. The cashier quickly enters their PIN code.
3. The cashier logs in without typing a username.

This design makes the cashier login process fast, simple, and suitable for a busy retail environment where speed matters.

### 3.2 Manager Login Section

The manager login section uses a traditional login method. It contains two fields:

- Username
- Password

This section is intended for managers or higher-level users who need to access the system through full credentials rather than quick PIN login.

### 3.3 Difference Between Cashier and Manager Login

The main difference between cashier login and manager login is the amount of information the user must enter.

For a cashier:

- The user does not need to type a username.
- The user selects their cashier card.
- The user enters a PIN code.
- The login flow is fast and simple.

For a manager:

- The user enters a username.
- The user enters a password.
- The login flow is more formal and credential-based.

This gives the application both speed for daily cashier operations and security for management access.

---

## 4. Login Display Logic

The login screen has special display behavior depending on the available users in the system.

### 4.1 When There Are No Cashiers

If there are no cashier users and only an administrator exists, the administrator appears in the cashier card area.

This allows the administrator to still access the system through the visible user card section when no cashier users have been created yet.

### 4.2 When There Is at Least One Cashier

If there is only one cashier, or if cashier users exist in the system, the administrator disappears from the cashier card section. In that case, only cashier users are shown in the cashier login area.

The intended logic is:

- If no cashiers exist, show the administrator in the cashier card location.
- If cashier users exist, hide the administrator from the cashier card location and show only cashier users.

This behavior keeps the cashier login area focused on actual cashier users once the store has active cashier accounts.

---

## 5. First Login and Register Status

After logging in for the first time, the user sees that the register is not open. The system requires the user to open the register before starting sales operations.

The application displays a register-related button or action. When the user clicks it, the system opens the register workflow.

The register opening process is important because it starts the cashier session and records the opening cash amount.

---

## 6. Opening the Register

When the user selects the register button, the system presents the **Open Register** or **Open Cashier Register** screen.

The user can enter the following information:

- Opening cash amount
- Optional note

### 6.1 Opening Cash Amount

The opening cash amount represents the cash available in the drawer at the beginning of the register session.

If the user has opening cash, they can enter the amount before opening the register.

If the user does not have opening cash, they can simply press **Open Register** without entering an amount or extra details.

### 6.2 Opening Note

The note field allows the user to explain what the opening money is related to. For example, the note can describe the source or reason for the money placed in the register at the beginning of the session.

### 6.3 Opening Action

After entering the opening cash amount and optional note, or after leaving the amount empty if no cash exists, the user presses **Open Register**.

Once the register is open, the cashier can proceed to the checkout page and begin processing sales.

---

## 7. Checkout Page Overview

The checkout page is the first main operational screen that appears after the register is open.

The checkout page is designed to help the cashier complete sales quickly and efficiently. The screen focuses on fast product selection, clear cart visibility, easy payment entry, receipt handling, and immediate sale completion.

The checkout workflow is built around speed and usability. It is intended to reduce friction for the cashier and make the payment process faster for the customer.

---

## 8. Checkout Page Layout

The checkout page uses a **70/30 layout**.

### 8.1 Product Area: 70 Percent

Approximately 70 percent of the screen is dedicated to the product area. This is where the cashier can view products.

The product area shows product information such as:

- Product image
- Product name

This section allows the cashier to quickly identify and select items during checkout.

### 8.2 Cart Area: 30 Percent

Approximately 30 percent of the screen is dedicated to the cart area. This area contains the current transaction details.

The cart gives the cashier a clear view of the active sale, including selected items, totals, taxes, payment status, and checkout actions.

---

## 9. Mouse and Touchscreen Support

The current checkout layout works well on a regular computer using a mouse.

The design is also suitable for touchscreen usage, and the layout supports touch interaction. However, the application is not yet fully converted or optimized for touchscreen-only usage.

Current status:

- Mouse usage is supported.
- Touchscreen usage is supported.
- Touchscreen layout is suitable but not yet perfect.
- Full touchscreen optimization is still pending.

This means the application can currently support both mouse users and touchscreen users, while future improvements may further refine the touchscreen experience.

---

## 10. Cart Features

The cart area contains the key financial and item information needed by the cashier during checkout.

The cart includes the following details:

- Grand total
- Number of products
- Taxes, if applicable
- Price amount needed from the customer
- Current cart items

The cart is intended to show exactly the information a cashier needs during a sale.

### 10.1 Remove Items from Cart

The cashier can remove items from the cart when needed. This allows correction of mistakes or changes before completing the sale.

### 10.2 Add Discount to the Whole Cart

The cashier can apply a discount to the whole cart.

The discount can be entered in two ways:

- Percentage discount
- Fixed amount discount

This gives flexibility for promotions, manual discounts, customer adjustments, or manager-approved price changes.

---

## 11. Payment Entry Features

The checkout page includes tools that make entering payments faster.

### 11.1 Exact Amount Button

For users working with a mouse, the checkout page includes an **Exact** button.

When the cashier presses the Exact button, the system automatically enters the exact total amount required from the customer.

This prevents the cashier from manually typing the payment amount when the customer pays the exact total.

### 11.2 Quick Value Buttons

The checkout page includes quick value buttons for common payment amounts.

These buttons allow the cashier to quickly enter commonly used payment values, such as:

- $5
- $20
- $100

The exact values can depend on the type of money commonly used in the store. These are called **Quick Value Buttons** because they allow the cashier to customize and enter payment amounts quickly.

The purpose of these buttons is to reduce typing and speed up checkout.

### 11.3 Change Label

The checkout page includes a change label that displays payment balance information.

The label shows one of the following situations:

- How much money is still left to pay
- How much change should be returned to the customer

This gives the cashier immediate feedback after entering a payment amount.

---

## 12. Completing the Sale

The checkout page includes a **Complete Sale** button.

When the cashier presses Complete Sale, the system completes the transaction.

After completing the sale, the cashier can choose how to handle the receipt.

Receipt options include:

- Print the receipt to the screen
- Print or export the receipt as a PDF
- Print the receipt using an actual printer

This gives the store flexibility depending on whether the customer needs a printed receipt, digital receipt, or visible confirmation on screen.

---

## 13. Receipt Handling

The checkout screen includes receipt-related functionality.

The cashier can press the receipts option if they want to view or manage receipts.

When the receipt or completed-sale result appears, the screen displays important payment information for the cashier.

One key display element is the **Change Due** amount.

### 13.1 Change Due Display

After a sale, the change due is displayed in the middle of the screen.

The change due appears in green text.

This makes the amount visually clear so the cashier can quickly return the correct change to the customer.

---

## 14. Payment Notifications and Remaining Amounts

The system can show notifications if there is any amount of money left unpaid.

For example, if the customer has not paid the full amount, the system can show the remaining balance. This helps prevent the cashier from completing or misunderstanding a payment when the entered amount is lower than the total due.

The payment feedback is designed to clearly show whether:

- The amount entered is enough.
- More money is still required.
- Change should be returned to the customer.

---

## 15. Inventory Alerts During Checkout

The checkout screen also supports inventory alerts.

If a product has low inventory, the system alerts the cashier. The alert tells the cashier which products are low and how many units are left in the shelf or inventory.

This helps the store avoid manually checking every product during checkout.

The inventory alert can show:

- The product that is low
- The remaining quantity
- The product status in inventory or on the shelf

This feature supports better stock awareness while the cashier is processing sales.

---

## 16. Register Closing

The cashier can close the register when the session is finished.

When closing the register, the system displays a summary of the register session.

The closing screen includes the following information:

- Count of orders made during the session
- Total amount of orders made during the session
- Cash sales amount
- Card sales amount, if applicable
- Opening money entered at the beginning of the session
- Cash in amounts
- Cash out amounts
- Difference between expected and counted cash
- Closing notes

### 16.1 Order Count

The system shows the number of orders completed during the entire register session.

This allows the cashier or manager to understand how many transactions were processed before closing.

### 16.2 Order Amount

The system shows the total amount of orders made during the session.

This amount is spelled out and separated by payment type when applicable.

### 16.3 Cash and Card Breakdown

The register closing screen shows totals for:

- Cash payments
- Card payments, if card payments exist

This makes reconciliation easier and helps the store compare physical cash with recorded system totals.

### 16.4 Opening Money

The closing screen shows the opening money that was entered when the register was opened.

This is important because the opening amount affects the expected cash total at the end of the session.

### 16.5 Cash In and Cash Out

The system tracks cash movements during the register session.

The cashier can record:

- Cash in
- Cash out

These entries help explain why the cash drawer amount may change during the session outside of normal sales.

### 16.6 Difference Calculation

When closing the register, the system compares the expected amount with the counted cash amount.

If the counted cash is above the expected amount, the system shows the difference as an overage.

If the counted cash is below the expected amount, the system shows the difference as a shortage.

For example, if the counted cash is above by $50, the system shows that the register is above by $50. If the counted cash is below by $50, the system shows that it is less by $50.

This helps identify whether the drawer is balanced, over, or short.

### 16.7 Closing Notes

The cashier can add notes while closing the register.

These notes can explain anything unusual about the session, including cash differences, special situations, corrections, or operational details.

---

## 17. Cash In and Cash Out Operations

The application allows the cashier to perform cash in and cash out operations from the register.

This means the cashier can record additional money added to the drawer or money removed from the drawer during the session.

All cash in and cash out activity is saved in the system. This keeps the register history clean, organized, and traceable.

This feature helps maintain accurate financial records during daily store operations.

---

## 18. Products Page

The **Products** page is used to manage the store’s product catalog and inventory-related product information. It allows the user to add products, import and export product data, refresh the product list, search for products, clear search input, edit products, delete products, and transfer stock from warehouse quantity to store or shelf quantity.

The Products page is designed for product management and stock visibility. It gives the user a detailed view of selected products while also supporting quick operational actions such as stock transfer.

---

## 19. Products Page Main Actions

The Products page contains the following main actions:

- Add Product
- Import Products
- Export Products
- Refresh Products
- Clear Search

### 19.1 Add Product

The **Add Product** action allows the user to create a new product in the system.

This is used when the store needs to add a new item to the product catalog.

### 19.2 Import Products

The **Import Products** action allows the user to bring product data into the system from an external source.

This is useful when adding many products at once instead of creating them manually one by one.

### 19.3 Export Products

The **Export Products** action allows the user to export product data from the system.

This can be used for backup, reporting, external review, or product data transfer.

### 19.4 Refresh Products

The **Refresh Products** action reloads or updates the product list currently shown on the Products page.

This helps the user make sure they are seeing the latest available product information.

### 19.5 Clear Search

The **Clear** button is used when the user is currently searching for a product.

If a product name or search term has been entered, pressing Clear removes the search text and resets the search field.

---

## 20. Products Page Layout

The Products page is divided into two main functional areas:

1. Product list and management area on the left side
2. Selected product detail area on the right side

### 20.1 Left Side: Product List and Management

The left side contains the products that the user wants to manage.

The user can select a product from the list. After selecting a product, the user can press the **Edit** button to modify the product information.

The basic workflow is:

1. Search for or locate the product.
2. Select the product.
3. Press Edit.
4. Update the product details.

This makes the left side the main navigation and selection area for product management.

### 20.2 Right Side: Product Detail Layout

The right side displays the details of the selected product.

The product detail area shows the following information:

- Product photo
- Barcode
- Cost price
- Sell price
- Store quantity
- Warehouse quantity
- Store theoretical quantity
- Warehouse theoretical quantity
- Last sale information
- Last purchase added information
- Tax configuration

This gives the user a complete operational view of the selected product, including pricing, stock levels, barcode information, tax setup, and historical sales or purchase-related details.

---

## 21. Product Delete Function

The Products page includes a **Delete** button.

The Delete button is used to delete the selected product from the system.

This action is intended for removing products that should no longer exist in the product catalog.

---

## 22. Transfer Stock Feature

The Products page includes an important operational feature called **Transfer Stock**.

This feature helps the cashier or store user work faster when moving products from warehouse stock to store or shelf stock.

Instead of editing the product manually, the user can press the Transfer Stock button and select how many items should be transferred.

The transfer stock workflow is:

1. Select the product.
2. Press the **Transfer Stock** button.
3. Enter or select the quantity to transfer.
4. Press **Transfer**.
5. The system automatically handles the stock movement.

This means that if a product exists in the warehouse and needs to be moved to the shelf or store quantity, the user does not need to manually edit the product record.

The application handles the stock transfer automatically and updates the relevant quantities.

This feature is designed to save time, reduce manual mistakes, and make stock movement easier during daily store operations.

---

## 23. Products Page Low Threshold Notification

The Products page includes a notification related to low-stock thresholds.

This notification shows how many products are currently below the threshold in the shelf or store stock.

The notification does not directly list the exact products. Instead, it tells the user how many products are below the threshold.

The user can then search for those products and refill or transfer stock as needed.

The purpose of this notification is to help the user quickly understand that some products need attention without manually checking every product.

This helps the store identify products that may need to be replenished and supports better shelf availability.

---

## 24. Reports Page

The **Reports** page contains two main tabs:

1. Dashboard tab
2. Receipts tab

The Reports page is used for analysis, sales review, stock status review, receipt filtering, and operational reporting.

The Dashboard tab focuses on fixed daily and weekly analysis. The Receipts tab is more flexible and allows date filtering.

---

## 25. Dashboard Tab

The **Dashboard** tab is one of the most important pages for business analysis.

It provides a fixed analytical view focused on today’s performance, yesterday’s comparison, weekly sales visibility, target tracking, top products, discounts, critical stock alerts, low-stock products, and stock health.

The Dashboard includes the following major areas:

- Sales Today
- Net Profit Today
- Invoice Count
- Average Invoice Value and Median
- Weekly chart
- Sales vs Target
- Top Selling Products for Today
- Discounts Today
- Critical Alerts
- Low Stock and Out of Stock card
- Stock Health

---

## 26. Dashboard Summary Cards

The dashboard includes four important summary cards:

1. Sales Today
2. Net Profit Today
3. Invoice Count
4. Average Invoice Value and Median

These cards provide the main high-level performance indicators for the store.

### 26.1 Sales Today

The **Sales Today** card shows the total sales amount made during the current day.

It also compares today’s sales with the previous day.

This helps the user quickly understand whether today’s sales are better or worse than yesterday’s sales.

### 26.2 Net Profit Today

The **Net Profit Today** card shows the net profit amount for the current day.

It also shows the total margin made for today.

This gives the user visibility not only into revenue, but also into actual profitability.

### 26.3 Invoice Count

The **Invoice Count** card shows the number of invoices created during the whole day.

This value is based on the full day and is not limited to a single register session.

The purpose of this card is to show how many completed sales or invoices were created across the day.

### 26.4 Average Invoice Value and Median

The **Average Invoice Value** shows the average value of invoices.

However, the average invoice value may not always be fully accurate as a business indicator because some invoices may be very high while others may be very low.

To solve this, the card also shows the **median** invoice value.

The median helps represent the middle invoice value and gives a more balanced view when invoice amounts are uneven.

The dashboard shows both average and median values so the user can compare them.

---

## 27. Weekly Chart

The dashboard includes a chart for the whole week.

This chart gives the user a visual overview of sales activity across the week.

The Sales Today card compares today with yesterday, but it does not compare today with the whole week. The weekly chart fills that gap by showing a broader visual trend.

The chart is intended as a visual reference. It does not focus on detailed numbers. The user can look at it quickly to understand the weekly pattern.

---

## 28. Sales vs Target Card

The **Sales vs Target** card is used when the store has a daily sales target.

This card shows how much of today’s target has been achieved.

It displays:

- Percentage reached for today’s target
- Total amount made today
- Target amount
- Yesterday’s performance

This helps the user track daily performance against a required goal.

### 28.1 Sales vs Target Progress Bar

The Sales vs Target card includes a progress bar that combines three values in one visual element:

1. Today’s progress
2. Yesterday’s progress
3. Target line

The progress bar uses the following visual logic:

- The blue bar represents today.
- The gray bar represents yesterday.
- The final line represents the target.

If today’s performance goes above yesterday’s performance, the blue bar can override or cover the gray bar.

If yesterday’s performance is above or equal to the target, the user may see a gray progress bar with the blue bar overriding it depending on today’s progress.

This gives the user a compact way to compare today, yesterday, and the target in one place.

---

## 29. Top Selling Products for Today

The dashboard includes a **Top Selling Products for Today** section.

This section shows the top five products sold today.

Each product includes a progress bar that shows the percentage contribution of that product for the day.

This helps the user understand where today’s sales are coming from and which products are driving the most activity.

---

## 30. Discounts Today

The dashboard includes information about discounts made today.

It also includes a badge on the right side that compares today’s discounts with yesterday’s discounts.

This allows the user to quickly understand whether discount activity is higher or lower than the previous day.

---

## 31. Critical Alerts

The dashboard includes a **Critical Alerts** section.

This section contains sensitive and important stock information that the user needs to monitor.

Critical alerts include:

- Shelf low stock
- Warehouse low stock
- Shelf empty
- Warehouse empty

These alerts help the user identify urgent stock issues that may affect store operations.

---

## 32. Low Stock and Out of Stock Card

The dashboard includes a detailed **Low Stock / Out of Stock** card.

This card shows approximately the top eight products that need attention.

The products are calculated and ordered based on emergency level.

The highest priority is given to products that are out of stock. After that, the system focuses on products that are low in stock.

Priority logic:

1. Out of stock products are shown first because they are the most urgent.
2. Low stock products are shown after out of stock products.

This card helps the user focus on the most urgent stock problems first.

---

## 33. Stock Health Card

The dashboard includes a **Stock Health** card.

This card shows the percentage health of the store’s stock.

If the stock is fully healthy and there are no products below the low-stock threshold, the system shows a blue circle with **100%**.

A 100% value means the stock is clean and there are no low-threshold issues.

The Stock Health card uses three status colors:

- Healthy stock shown in blue
- Low stock shown in yellow
- Out of stock shown in red

This gives the user a quick visual understanding of the overall inventory condition.

---

## 34. Receipts Tab

The **Receipts** tab is more flexible than the Dashboard tab.

The Dashboard is fixed around today and the current week. The user cannot freely customize the Dashboard period.

The Receipts tab, however, allows the user to choose a date range.

Available receipt filters include:

- Today
- Seven days
- Thirty days
- Custom date range

This allows the user to review receipts across different periods depending on the analysis or operational need.

### 34.1 X Report Generation

The Receipts tab allows the user to generate the **X Report** for today only.

This means the X Report is specifically tied to the current day and is not generated for custom historical ranges.

---

## 35. Updated Functional Scope Summary

Based on the current description, Retail Store POS includes the following documented capabilities:

1. Windows desktop support for Windows 10 and Windows 11.
2. Splash screen during startup.
3. Two-section login page.
4. Fast cashier login using user cards and PIN code.
5. Manager login using username and password.
6. Conditional display of administrator and cashier cards.
7. Register not-open state after login.
8. Open register workflow with opening cash amount and note.
9. Checkout page as the main operational screen.
10. 70/30 checkout layout split between products and cart.
11. Product display with images and names.
12. Cart display with grand total, product count, taxes, and required payment amount.
13. Item removal from cart.
14. Whole-cart discount by percentage or fixed amount.
15. Exact amount payment button.
16. Customizable quick value payment buttons.
17. Change or remaining amount display.
18. Complete sale action.
19. Receipt display, PDF printing, and physical printer support.
20. Change due display in green text at the center of the screen.
21. Payment notifications for remaining balance.
22. Inventory alerts for low-stock products.
23. Register closing workflow.
24. Closing summary with order count and order totals.
25. Cash and card breakdown.
26. Opening money tracking.
27. Cash in and cash out tracking.
28. Register difference calculation.
29. Closing notes.
30. Saved and organized register activity.
31. Products page with add, import, export, refresh, and clear search actions.
32. Product detail view with photo, barcode, pricing, quantities, theoretical quantities, last sale, last purchase, and tax configuration.
33. Product editing and deletion.
34. Transfer stock workflow from warehouse to store or shelf quantity.
35. Products page low-threshold notification.
36. Reports page with Dashboard and Receipts tabs.
37. Dashboard sales analysis for today and weekly visual reference.
38. Sales today, net profit today, invoice count, average invoice value, and median invoice value cards.
39. Sales vs Target card with today, yesterday, and target progress in one visual bar.
40. Top five selling products for today.
41. Discounts comparison between today and yesterday.
42. Critical stock alerts for shelf and warehouse stock.
43. Low Stock / Out of Stock prioritization card.
44. Stock Health card with blue, yellow, and red status indicators.
45. Receipts tab with Today, Seven Days, Thirty Days, and Custom date filters.
46. X Report generation for today only.

---

## 36. Settings Page

The **Settings** page contains the administrative and configuration areas of the application. It allows the user to control store-level information, tax behavior, dashboard targets, user-related preferences, and other system-level configuration.

The Settings page includes several sections, including:

- Store Management
- Tax Configuration
- Users and Roles
- My Preference
- About
- Sign Out

This section currently documents **Store Management** and **Tax Configuration**. User Management and the remaining settings areas will be documented after more details are provided.

---

## 37. Store Management Settings

The **Store Management** section contains the general settings for the store.

This section is used to define store identity, location information, regional behavior, currency behavior, and dashboard targets.

Store Management includes the following settings:

- Store name
- Store address
- Region or country selection
- Currency selection behavior
- Dashboard daily sales target
- Save settings button

### 37.1 Store Name

The store name setting allows the user to define the official name of the store inside the application.

This name may be used throughout the system, including receipts, reports, store profile areas, and general application configuration.

### 37.2 Store Address

The store address setting allows the user to enter the address of the store.

This information may be used for receipts, business records, reports, or internal store identification.

### 37.3 Region and Country Selection

The Store Management section includes a region or country selector.

The purpose of selecting a country is to help the application determine or suggest the appropriate currency and regional behavior for the store.

However, the region and currency are not necessarily locked together. For example, a store may be operating in the United States region while still dealing with sterling instead of United States dollars.

This means the system should support practical currency flexibility rather than assuming that the selected country always determines the final currency used by the application.

### 37.4 Currency Configuration

The currency setting controls which currency is used by the application for the customer and store operations.

The application should allow the user to choose the currency that matches their real business use case, even if that currency differs from the selected country or region.

This is important for stores that operate across regions, use foreign currency, or serve customers in a currency different from the local default.

### 37.5 Dashboard Daily Sales Target

Store Management includes the dashboard setting for the **Daily Sales Target**.

This value is used by the Dashboard page, specifically by the **Sales vs Target** card described earlier in this report.

The daily sales target allows the store to define a sales goal for the day. The dashboard then compares today’s sales against that target and also compares performance with yesterday.

### 37.6 Save Settings Button

The Store Management section includes a **Save** button.

After the user edits store settings, pressing Save stores the changes and applies the updated configuration.

---

## 38. Tax Configuration Settings

The **Tax Configuration** section is one of the most complex parts of the application.

It controls how taxes are enabled, calculated, rounded, assigned to agencies, applied to products or orders, and grouped into product tax profiles.

By default, the tax configuration is not active. To use the tax configuration features, the user must enable the tax processing engine.

---

## 39. Enable Tax Processing Engine

Tax configuration is not open or active by default.

To enable tax functionality, the user must turn on the **Tax Processing Engine**.

Once enabled, the application can process advanced tax rules, agencies, rounding behavior, inclusive and exclusive taxes, product-specific taxes, order-level taxes, and product tax profiles.

If the tax processing engine is not enabled, the system should not apply the advanced tax configuration rules.

---

## 40. Legacy Default Tax Rate

The Tax Configuration section includes a **Legacy Default Tax Rate** option.

This option allows the user to define a default tax rate if they want to use one.

If the user does not want to use the legacy default tax rate, they can ignore it.

This option appears to exist for simpler tax behavior, backward compatibility, or stores that do not need the full advanced tax rules system.

---

## 41. Global Tax Rounding Behavior

The Tax Configuration section includes global rounding behavior for taxes.

This is important because tax calculations can create decimal values with more precision than the application displays or charges.

For example, the application may handle two digits, three digits, four digits, or five digits during calculation. A tax value may produce a number such as **1.0543**. The system must decide how to round that value before charging the customer or recording the tax.

Without clear rounding behavior, many small tax differences can accumulate across many products and many sales. At the end of the day, those small differences may create missing pennies, extra pennies, or reconciliation differences.

The tax rounding strategy exists to control who absorbs or receives those small decimal differences and to support different legal requirements across countries.

---

## 42. Tax Rounding Strategy

The **Tax Rounding Strategy** controls how tax amounts are rounded when the calculated tax produces more decimal precision than the final payable amount.

The available strategies include:

- Standard half up
- Banker’s half even
- Always round up
- Always round down

### 42.1 Standard Half Up

The **Standard Half Up** strategy rounds using the common half-up method.

For example, if the tax is **1.0543**, the system checks the next digit after the required decimal position. If that digit does not require rounding up, the system drops the extra digits and keeps the current value.

If the tax value reaches a half value that requires rounding, the system rounds up.

For example, if the relevant value reaches **1.055**, the next digit causes the amount to round upward, making the final payable amount higher by the smallest required currency unit.

This rounded amount is charged to the customer.

### 42.2 Always Round Down

The **Always Round Down** strategy always rounds the tax amount downward.

If this strategy is selected, the store may be responsible for the small difference at the end of the day because the customer is not charged the rounded-up penny or minor currency unit.

This behavior may be appropriate in some countries or business rules, depending on tax requirements.

### 42.3 Always Round Up

The **Always Round Up** strategy always rounds the tax amount upward.

This makes sure the calculated tax is never reduced by rounding down. Depending on local rules, this may or may not be appropriate.

### 42.4 Banker’s Half Even

The **Banker’s Half Even** strategy is another supported rounding method.

This method is often considered a strong option for handling repeated rounding because it helps reduce rounding bias over many transactions.

The application supports this method because different countries and accounting requirements may prefer different rounding behavior.

---

## 43. Cash Rounding Unit

The Tax Configuration section also includes a **Cash Rounding Unit** setting.

This is related to rounding the final cash amount rather than only rounding the tax value.

Cash rounding is useful when a currency or country does not commonly use very small coins, or when the business wants final cash totals rounded to practical payment units.

Available cash rounding units include:

- Exact, meaning one penny or the smallest currency unit
- Round to 5 cents
- Round to 10 cents
- Round to 50 cents

The user can choose which rounding unit should apply.

For example, if the user chooses rounding to 5 cents, the final amount is rounded to the nearest valid 5-cent increment according to the selected rounding behavior.

This setting helps avoid cash totals that are difficult or impossible to pay physically when certain small currency units are not used.

---

## 44. Tax Agencies

The **Tax Agencies** section defines who collects or owns the tax.

A tax agency may represent one of the following:

- Government
- State
- County
- City
- Another tax-collecting authority

The purpose of tax agencies is to identify where collected taxes belong.

For example, a product may include taxes collected for a federal authority, a city authority, and a county authority. Each agency can be defined separately so the application can organize and apply taxes correctly.

---

## 45. Tax Rates and Tax Rules

The Tax Configuration section includes **Tax Rates** or **Tax Rules**.

A tax rule defines how a tax is calculated and where it applies.

Each tax rule may include the following information:

- Tax name
- Calculation type
- Inclusive or exclusive behavior
- Tax scope

### 45.1 Tax Name

The tax name identifies the tax rule.

This helps the user recognize the tax in the system and understand which tax is being applied.

### 45.2 Calculation Type

The application supports multiple tax calculation types.

Supported calculation types include:

- Standard percentage
- Fixed amount
- Tiered rates
- Per unit or weight
- Percentage on margin
- Reverse charge

#### Standard Percentage

The standard percentage calculation applies tax as a percentage of the relevant taxable amount.

#### Fixed Amount

The fixed amount calculation applies a fixed tax amount instead of a percentage.

#### Tiered Rates

Tiered rates allow tax to change based on defined levels, ranges, or thresholds.

#### Per Unit or Weight

The per unit or weight calculation applies tax based on quantity, unit count, or weight.

#### Percentage on Margin

The percentage on margin calculation applies tax based on the margin rather than the full selling price.

#### Reverse Charge

The reverse charge calculation is used for business-to-business tax situations in specific countries.

This supports cases where tax responsibility may shift between parties according to local rules.

### 45.3 Inclusive or Exclusive Tax

The user can define whether the tax is inclusive or exclusive.

If the tax is marked as inclusive, the tax is already included in the product price.

If the tax is left unmarked or not set as inclusive, it is treated as exclusive. In that case, the tax is added on top of the product price or order amount.

### 45.4 Tax Scope

The user can define the scope of the tax.

Supported scopes include:

- Per product
- Entire order or subtotal

A product-level tax applies to individual products. An order-level or subtotal tax applies to the full order amount.

---

## 46. Product Tax Profiles

The Tax Configuration section includes **Product Tax Profiles**.

Product tax profiles allow the user to combine more than one tax into a single profile that can then be applied to a product.

This is important because one product may need multiple taxes applied at the same time.

For example, a product may require:

- County tax
- City tax
- Federal tax

Instead of assigning each tax separately every time, the user can combine them into one product tax profile. That profile can then be applied to the product.

This makes tax management cleaner, faster, and more reliable.

The product tax profile concept allows the application to group multiple tax rules together and apply them consistently to products.

---

## 47. Updated Functional Scope Summary

Based on the current description, Retail Store POS includes the following documented capabilities:

1. Windows desktop support for Windows 10 and Windows 11.
2. Splash screen during startup.
3. Two-section login page.
4. Fast cashier login using user cards and PIN code.
5. Manager login using username and password.
6. Conditional display of administrator and cashier cards.
7. Register not-open state after login.
8. Open register workflow with opening cash amount and note.
9. Checkout page as the main operational screen.
10. 70/30 checkout layout split between products and cart.
11. Product display with images and names.
12. Cart display with grand total, product count, taxes, and required payment amount.
13. Item removal from cart.
14. Whole-cart discount by percentage or fixed amount.
15. Exact amount payment button.
16. Customizable quick value payment buttons.
17. Change or remaining amount display.
18. Complete sale action.
19. Receipt display, PDF printing, and physical printer support.
20. Change due display in green text at the center of the screen.
21. Payment notifications for remaining balance.
22. Inventory alerts for low-stock products.
23. Register closing workflow.
24. Closing summary with order count and order totals.
25. Cash and card breakdown.
26. Opening money tracking.
27. Cash in and cash out tracking.
28. Register difference calculation.
29. Closing notes.
30. Saved and organized register activity.
31. Products page with add, import, export, refresh, and clear search actions.
32. Product detail view with photo, barcode, pricing, quantities, theoretical quantities, last sale, last purchase, and tax configuration.
33. Product editing and deletion.
34. Transfer stock workflow from warehouse to store or shelf quantity.
35. Products page low-threshold notification.
36. Reports page with Dashboard and Receipts tabs.
37. Dashboard sales analysis for today and weekly visual reference.
38. Sales today, net profit today, invoice count, average invoice value, and median invoice value cards.
39. Sales vs Target card with today, yesterday, and target progress in one visual bar.
40. Top five selling products for today.
41. Discounts comparison between today and yesterday.
42. Critical stock alerts for shelf and warehouse stock.
43. Low Stock / Out of Stock prioritization card.
44. Stock Health card with blue, yellow, and red status indicators.
45. Receipts tab with Today, Seven Days, Thirty Days, and Custom date filters.
46. X Report generation for today only.
47. Settings page with Store Management and Tax Configuration sections.
48. Store settings for store name, address, region, currency, and daily sales target.
49. Save button for applying store management changes.
50. Optional tax processing engine that must be enabled before advanced tax configuration is used.
51. Legacy default tax rate option.
52. Global tax rounding strategy.
53. Supported tax rounding methods: standard half up, banker’s half even, always round up, and always round down.
54. Cash rounding unit options: exact, 5 cents, 10 cents, and 50 cents.
55. Tax agencies for government, state, county, city, or other tax authorities.
56. Tax rules with name, calculation type, inclusivity, and scope.
57. Tax calculation types including standard percentage, fixed amount, tiered rates, per unit or weight, percentage on margin, and reverse charge.
58. Product tax profiles that combine multiple tax rules and apply them to products.

---

## 48. Top-Right Cashier Button and User Menu

The application includes a top-right cashier button located near the window control buttons, including minimize and maximize.

This button acts like a user card. It displays cashier and register status information and provides quick access to register and logout actions.

The top-right cashier button contains the following information:

- Cashier name
- Cashier photo placeholder
- Active register status

At the moment, the cashier photo is not fully set yet, so the photo area exists but may not display an actual user image.

The button also shows whether there is an active register or not. This gives the user quick visibility into the current register state.

---

## 49. Top-Right User Dropdown Menu

When the user presses the top-right cashier card, the application opens a dropdown menu.

The available actions depend on whether there is an active register.

### 49.1 Active Register Actions

If there is an active register, the dropdown menu enables register-related actions.

The menu can include:

- Cash In / Cash Out action
- Close Register action
- Logout action

The **Cash In / Cash Out** action gives the user quick access to register cash movement without needing to navigate elsewhere.

The **Close Register** action allows the user to close the active register from the dropdown.

### 49.2 Logout Shortcut

At the end of the dropdown menu, there is a **Logout** button.

This acts as a quick logout shortcut. It allows the user to log out from the top-right menu instead of using the logout button located at the bottom-left side of the screen.

This gives users two logout access points:

- Top-right user dropdown logout
- Bottom-left logout button

---

## 50. Manage Users Page

The **Manage Users** page is used to view, manage, and edit application users, depending on permission level.

This page follows the same general layout concept used elsewhere in the application, using a **70/30 layout**.

The page is divided into:

1. Left user list area
2. Right user detail area

### 50.1 Left Side: User List

The left side takes approximately 30 percent of the layout and displays all users.

The list can include different user types, such as:

- Administrator
- Cashier
- Manager or other configured roles

Each user entry includes a badge that shows the type or role of the user. The badge is displayed in green.

This allows the administrator or authorized user to quickly identify whether a user is an admin, cashier, or another role.

### 50.2 Right Side: Read-Only User Profile

When a user is selected from the left side, the right side displays that user’s profile details.

By default, the profile appears as a read-only page.

The right side contains several profile sections, including:

- Identity
- Security
- Role and Permissions

The profile can only be edited after unlocking it, and only if the current user has the required permission.

---

## 51. Manage Users: Identity Section

The **Identity** section contains the main identifying information for the selected user.

It includes:

- Display name
- User or employee ID

### 51.1 Display Name

The display name is the visible name of the user.

Examples of display names include:

- Mark
- Adam
- Sarah
- Chanel
- Sophia

The display name is used to identify the cashier, administrator, or employee inside the application.

### 51.2 User or Employee ID

The user or employee ID is used as the username when signing in through the admin login section.

This connects the user management profile with the manager or administrator login system.

---

## 52. Manage Users: Security Section

The **Security** section contains login and access credentials for the selected user.

It includes:

- Four-digit PIN
- Admin password

### 52.1 Four-Digit PIN

The four-digit PIN is used for fast cashier login.

The PIN is limited to four digits only.

This supports the fast login workflow described earlier, where a cashier selects their user card and enters a quick PIN instead of typing a username.

### 52.2 Admin Password

The admin password is used for administrator-level access.

This field is only available when the user has administrator permission or admin-related access.

If the user does not have administrator permission, the admin password option is not opened or available for that user.

---

## 53. Manage Users: Roles and Permissions

The **Roles and Permissions** section controls what each user is allowed to access or modify inside the application.

The permissions define whether the user can access checkout, view reports, manage products, change prices, enter settings, or perform administrator-level actions.

The documented permissions include:

- Administrator access
- Process and Checkout
- Override Price
- View Reports
- Manage Products
- Manage Settings

### 53.1 Administrator Access

The **Administrator Access** permission gives the user full administrator control.

A user with administrator access can manage the application at the highest level, depending on the system’s security rules.

### 53.2 Process and Checkout

The **Process and Checkout** permission controls access to the checkout page.

If a user has this permission, they can access and use the checkout page.

If a user does not have this permission, they cannot access the checkout page.

### 53.3 Override Price

The **Override Price** permission controls whether the user can change or override prices.

If a user has this permission, they can override prices in the product-related workflow.

If a user does not have this permission, they cannot change product prices.

This protects product pricing from unauthorized changes.

### 53.4 View Reports

The **View Reports** permission controls access to the Reports page.

If a user has this permission, they can access and view reports.

If a user does not have this permission, they cannot access or even view the Reports page.

### 53.5 Manage Products

The **Manage Products** permission controls access to product management.

This permission applies to the Products page as a whole.

A user may be allowed to access product management actions such as changing quantities or managing stock, while still being restricted from sensitive actions such as price changes if they do not have the Override Price permission.

This allows product access and price access to be controlled separately.

### 53.6 Manage Settings

The **Manage Settings** permission controls access to administrative settings pages.

If the user does not have this permission, they cannot access key settings areas, including:

- Store Management
- Tax Configuration
- Users and Roles

This protects major application configuration areas from unauthorized access.

A normal cashier does not have access to Manage Users or other protected settings pages unless permission is granted.

---

## 54. Unlock Profile and Edit Mode

On the Manage Users page, the top-right corner contains an **Unlock** button.

This button is used to unlock the selected profile for editing.

The workflow is:

1. Select a user profile.
2. View the profile in read-only mode.
3. Press **Unlock** or **Unlock Profile**.
4. If the current user has permission, the profile becomes editable.
5. The authorized user can edit identity, security, roles, permissions, and related user settings.

If the current user does not have the required permission, the profile cannot be edited.

This protects user accounts and permissions from unauthorized changes.

---

## 55. My Preference Page

The **My Preference** page is used for personal cashier-level preferences.

The documented purpose of this page is to allow users to edit the quick cash buttons used during checkout.

These are the same quick value buttons described earlier in the checkout payment section.

### 55.1 Quick Cash Button Values

The My Preference page allows the user to enter three quick cash values.

These values define the three quick payment buttons shown on the checkout page.

For example, the user may configure common payment values such as:

- 5
- 20
- 100

The exact values can be customized based on what the cashier commonly uses.

### 55.2 Permission Behavior

The My Preference page does not require special administrative permission.

Any cashier can edit their own quick cash values because this setting helps the cashier work faster and does not affect sensitive system configuration.

This makes the feature personal, practical, and safe for regular cashier access.

---

## 56. About Page

The **About** page contains general application information and legal or informational content.

It includes information such as:

- Terms
- Policies
- Privacy information
- Other application-related details

This page gives users access to important information about the application, its policies, and privacy-related content.

---

## 57. Logout Button

The application includes a logout button.

The logout button is available from the bottom-left area of the screen.

The user can also access logout from the top-right user dropdown menu.

This gives users both a standard logout location and a quick-access logout option.

---

## 58. Visual UI Observations from Screenshots

The provided screenshots confirm the visual structure, labels, layout behavior, and several UI details that were not fully visible from the spoken description alone.

### 58.1 Application Branding and Shell

The application is branded as **Retail Store** and includes the **Nexill** logo in the top-left area.

The window header shows the current workspace name under the application title. Examples include:

- Checkout workspace
- Products workspace
- Reports workspace
- Store settings
- Tax configuration
- Users workspace
- My preferences
- About this app

The application uses a clean Windows desktop layout with a left vertical navigation rail, a top header area, and a large main content workspace.

### 58.2 Left Navigation Rail

The left navigation rail contains icon-only navigation items. The active section is marked by a green vertical indicator.

Visible navigation items include:

- Menu or navigation toggle
- Checkout
- Products
- Reports
- Settings
- Help or About
- Logout

This creates a compact navigation system that keeps the main workspace clear while still allowing quick movement between major application areas.

### 58.3 Top-Right User Card

The top-right user card displays the current signed-in user and register status.

In the screenshots, the visible user is **Administrator**.

The card includes:

- Circular avatar placeholder with the user initial
- User display name
- Register status text
- Status dot
- Dropdown arrow

The register status changes visually depending on the register state:

- **No active register** appears when the register is not open.
- **Register active** appears when the register is open.

When the register is not active, the dropdown shows Cash In / Out and Close Register as disabled actions, while Logout remains available.

### 58.4 Register Not Open Screen

The register not-open screen is centered and minimal.

It contains:

- Register or bank-style icon
- Main message: **Register is not open**
- Supporting text explaining that the cash register must be opened before selling
- Primary action button: **Open Register**

This screen prevents selling before the register session begins.

### 58.5 Open Cash Register Modal

The Open Cash Register modal appears over a dimmed background.

The modal includes:

- Title: **Open Cash Register**
- Helper text explaining that the user should enter the cash currently in the drawer to start the shift
- Opening Cash input
- Notes field labeled **Notes (Optional)**
- Primary button: **Open Register**
- Secondary button: **Discard**

The default opening cash value can be set to 0, allowing the user to open the register without entering additional cash.

### 58.6 Checkout Workspace Visual Details

The Checkout workspace confirms the 70/30 structure described earlier.

The left side contains the cart area, and the right side contains the product grid.

The product grid includes:

- Product search field
- Product count label, such as **150 PRODUCTS**
- Product cards with image and name

The cart area includes:

- Empty cart state
- Discount button
- Clear button
- Grand Total area
- Product count
- Tax and total information
- Payment entry row
- Exact button
- Quick-cash buttons
- Change display
- Complete Sale button

The quick-cash buttons shown in the screenshot use the configured currency and include values such as 5.00, 10.00, and 20.00 in Saudi Riyal formatting.

### 58.7 Products Workspace Visual Details

The Products workspace includes the following visible top actions:

- Add
- Import Data
- Export Data
- Refresh
- Clear

This confirms that the import and export actions are labeled as **Import Data** and **Export Data** in the current UI.

The page includes a yellow warning card showing the number of shelf products below threshold. In the screenshot, the warning reads:

**11 shelf products below threshold.**

The product list includes:

- Product name
- Barcode
- Store quantity
- Inventory or warehouse quantity
- Total quantity
- Sell price
- Unit label, such as pcs

The right-side product detail panel includes:

- Product image
- Delete button
- Transfer Stock button
- Edit button
- Barcode
- SKU
- Cost price
- Sell price
- Store quantity
- Warehouse quantity
- Store threshold
- Warehouse threshold
- Purchased at
- Last sale at
- Tax configuration

This confirms that threshold values are shown directly in the product detail panel, in addition to quantity values.

### 58.8 Transfer Stock Modal Visual Details

The Transfer Stock modal appears over a dimmed Products workspace.

The modal includes:

- Title: **Transfer Stock**
- Helper text: **Move items from Warehouse to Store.**
- Available in Warehouse value
- Current in Store value
- Quantity to move input
- Transfer button
- Cancel button

This confirms the stock transfer workflow is designed as a focused modal action rather than a full edit-page workflow.

### 58.9 Reports Dashboard Visual Details

The Reports page uses tabs at the top:

- Dashboard
- Receipts

The Dashboard begins with a greeting, such as:

**Good evening, Store Manager**

It also includes a date control on the right side.

The top dashboard cards include:

- Sales Today
- Net Profit Today
- Invoice Count
- Avg. Invoice Value

The cards include small visual trend lines and information icons.

The Avg. Invoice Value card also shows the median value below the main number.

Other visible dashboard cards include:

- Sales vs Target
- Top Selling Products
- Discounts Today
- Critical Alerts
- Low Stock / Out of Stock
- Stock Health

The Stock Health card is displayed as a donut chart with a percentage in the center and a legend showing Healthy, Low Stock, and Out of Stock categories.

### 58.10 Receipts Tab Visual Details

The Receipts tab includes a filter panel with:

- Quick Range options
- Today
- 7 Days
- 30 Days
- Generate X Report button
- From date
- To date
- Search field
- Clear search icon

The receipt table includes the following columns:

- Receipt number
- Date
- Time
- Type
- Subtotal
- Tax
- Total
- Action

Each receipt row includes a **Reprint** action.

The Receipts tab also shows the selected range and the number of transactions shown.

### 58.11 Store Management Visual Details

The Store Management page contains a Save button in the top-right corner.

Visible sections include:

- Store Identity
- Checkout Defaults
- Dashboard Settings

The Store Identity section includes:

- Store Name
- Store Address

The Checkout Defaults section includes:

- Region
- Currency

In the screenshot, the region is shown as **United States**, while the currency is shown as **SAR - Saudi Riyal**. This confirms the earlier requirement that region and currency can be configured independently.

### 58.12 Tax Configuration Visual Details

The Tax Configuration page contains a Save button in the top-right corner.

Visible sections include:

- Tax Processing Engine
- Global Rounding Behavior
- Tax Agencies
- Tax Rates & Rules
- Product Tax Profiles

The Tax Processing Engine section includes:

- Enable tax processing engine checkbox
- Legacy Default Tax Rate (%) input

The Global Rounding Behavior section includes:

- Tax Rounding Strategy dropdown
- Cash Rounding Unit dropdown

The Tax Agencies section uses the action label **Add Authority**.

The Tax Rates & Rules section uses the action label **Add Tax Rule**.

The Product Tax Profiles section uses the action label **Add Tax Group**.

Visible tax profile details include a **No Tax** profile marked as **DEFAULT**, followed by multiple rule rows using structured labels such as percentage, product, order, inclusive, exclusive, pre-discount, and post-discount.

### 58.13 Manage Users Visual Details

The Manage Users page shows a left roster and a right profile workspace.

The left side includes:

- Page title: **Manage Users**
- Helper text explaining that a team member can be selected to review or edit account details
- User count indicator
- User card with avatar placeholder
- Display name
- Username or employee ID
- Role badge

The right side initially shows an empty state:

- Title: **Select a User**
- Message: **No User selected**
- Helper text instructing the user to choose a user or add a new user

The page includes a **New user** button in the top-right area.

When a user is selected, the profile page includes:

- Profile title
- New user button
- Unlock button
- Identity section
- Security section
- Roles and permissions section
- Cancel Changes button
- Save Changes button

The Unlock action opens a confirmation modal titled **Unlock this profile?**. The modal explains that unlocking temporarily enables name, sign-in, password, PIN, and permission controls until the user saves or cancels.

### 58.14 My Preferences Visual Details

The My Preferences page contains the Quick-Cash Buttons configuration.

The visible text explains that the settings apply only to the current login and are designed for faster checkout.

The quick-cash values are shown as three fields:

- Value 1
- Value 2
- Value 3

The screenshot shows example values of 5, 10, and 20.

The page states that these settings auto-save directly.

### 58.15 About Page Visual Details

The About page displays the Nexill logo, the application name, and the application version.

Visible information includes:

- Application name: **Retail Store**
- Version: **1.4.0**
- Privacy and policy link or heading
- Product name: **Retail Store POS by Nexill**
- Terms of Use and Privacy Policy
- Effective date: **March 11, 2026**
- Last updated: **March 11, 2026**
- Support email: **contact@nexillretail.store**
- Privacy contact email: **contact@nexillretail.store**

The About page contains long-form policy text inside a scrollable content area.

---

## 59. Completed Functional Scope Summary

Based on the full description provided so far, Retail Store POS includes the following documented capabilities:

1. Windows desktop support for Windows 10 and Windows 11.
2. Splash screen during startup.
3. Two-section login page.
4. Fast cashier login using user cards and PIN code.
5. Manager login using username and password.
6. Conditional display of administrator and cashier cards.
7. Register not-open state after login.
8. Open register workflow with opening cash amount and note.
9. Checkout page as the main operational screen.
10. 70/30 checkout layout split between products and cart.
11. Product display with images and names.
12. Cart display with grand total, product count, taxes, and required payment amount.
13. Item removal from cart.
14. Whole-cart discount by percentage or fixed amount.
15. Exact amount payment button.
16. Customizable quick value payment buttons.
17. Change or remaining amount display.
18. Complete sale action.
19. Receipt display, PDF printing, and physical printer support.
20. Change due display in green text at the center of the screen.
21. Payment notifications for remaining balance.
22. Inventory alerts for low-stock products.
23. Register closing workflow.
24. Closing summary with order count and order totals.
25. Cash and card breakdown.
26. Opening money tracking.
27. Cash in and cash out tracking.
28. Register difference calculation.
29. Closing notes.
30. Saved and organized register activity.
31. Products page with add, import, export, refresh, and clear search actions.
32. Product detail view with photo, barcode, pricing, quantities, theoretical quantities, last sale, last purchase, and tax configuration.
33. Product editing and deletion.
34. Transfer stock workflow from warehouse to store or shelf quantity.
35. Products page low-threshold notification.
36. Reports page with Dashboard and Receipts tabs.
37. Dashboard sales analysis for today and weekly visual reference.
38. Sales today, net profit today, invoice count, average invoice value, and median invoice value cards.
39. Sales vs Target card with today, yesterday, and target progress in one visual bar.
40. Top five selling products for today.
41. Discounts comparison between today and yesterday.
42. Critical stock alerts for shelf and warehouse stock.
43. Low Stock / Out of Stock prioritization card.
44. Stock Health card with blue, yellow, and red status indicators.
45. Receipts tab with Today, Seven Days, Thirty Days, and Custom date filters.
46. X Report generation for today only.
47. Settings page with Store Management and Tax Configuration sections.
48. Store settings for store name, address, region, currency, and daily sales target.
49. Save button for applying store management changes.
50. Optional tax processing engine that must be enabled before advanced tax configuration is used.
51. Legacy default tax rate option.
52. Global tax rounding strategy.
53. Supported tax rounding methods: standard half up, banker’s half even, always round up, and always round down.
54. Cash rounding unit options: exact, 5 cents, 10 cents, and 50 cents.
55. Tax agencies for government, state, county, city, or other tax authorities.
56. Tax rules with name, calculation type, inclusivity, and scope.
57. Tax calculation types including standard percentage, fixed amount, tiered rates, per unit or weight, percentage on margin, and reverse charge.
58. Product tax profiles that combine multiple tax rules and apply them to products.
59. Top-right cashier button showing cashier name, photo placeholder, and active register status.
60. Top-right dropdown menu with Cash In / Cash Out, Close Register, and Logout actions when a register is active.
61. Quick logout access from the top-right dropdown.
62. Bottom-left logout button.
63. Manage Users page with a 70/30 layout.
64. User list with role badges.
65. Read-only user profile view by default.
66. User identity section with display name and user or employee ID.
67. Security section with four-digit PIN and admin password.
68. Four-digit PIN for fast cashier login.
69. Admin password for users with administrator permission.
70. Roles and permissions configuration.
71. Administrator Access permission for full admin control.
72. Process and Checkout permission for checkout access.
73. Override Price permission for product price changes.
74. View Reports permission for reports access.
75. Manage Products permission for product page access and product operations.
76. Manage Settings permission for Store Management, Tax Configuration, and Users and Roles access.
77. Unlock Profile button for switching from read-only mode to edit mode.
78. Permission-protected editing for user profiles.
79. My Preference page for cashier-level quick cash button settings.
80. Three configurable quick cash values.
81. Cashier-accessible preference editing without special administrative permission.
82. About page with terms, policies, privacy information, and application details.
83. Window control area near the top-right user button, including minimize and maximize controls.

---

## 59. Final Documentation Status

The currently described application areas have now been documented in this report:

- Startup and splash screen
- Login system
- Cashier and manager login behavior
- Register opening
- Checkout page
- Payment handling
- Receipt handling
- Inventory alerts
- Register closing
- Products page
- Reports page
- Dashboard
- Receipts tab
- Settings page
- Store Management
- Tax Configuration
- Top-right cashier button and dropdown menu
- Manage Users
- Roles and permissions
- My Preference
- About page
- Logout behavior

This report can continue to be expanded if additional implementation details, UI behavior, database structure, workflows, business rules, or technical architecture are provided later.

